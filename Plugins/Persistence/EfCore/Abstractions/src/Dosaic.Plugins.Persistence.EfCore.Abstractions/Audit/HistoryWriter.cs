using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Default <see cref="IHistoryWriter"/>. Walks the full <see cref="ChangeSet"/>, groups changes by
    /// (root type, root id), bundles them into a single <see cref="ObjectChanges"/> dictionary per root per save,
    /// and persists one <see cref="History{TRoot}"/> row per affected root.
    /// </summary>
    public sealed class HistoryWriter(IUserIdProvider userIdProvider, IDateTimeProvider dateTimeProvider,
        HistoryPathResolver pathResolver = null) : IHistoryWriter
    {
        private static readonly ConcurrentDictionary<Type, string[]> _rootExcludedAttrCache = new();
        private static readonly ConcurrentDictionary<Type, string[]> _rootChildNavCache = new();
        private static readonly ConcurrentDictionary<Type, string[]> _childExcludedCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> _calculateRootCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo> _calculateChildCache = new();
        private static readonly ConcurrentDictionary<Type, Func<NanoId, object>> _newIdFactoryCache = new();

        private static readonly MethodInfo _calculateOpenMethod = typeof(ObjectChanges)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(ObjectChanges.Calculate));

        private static readonly MethodInfo _calculateChildOpenMethod = typeof(ObjectChanges)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(ObjectChanges.CalculateChild));

        public async Task WriteAsync(ChangeSet changeSet, IDb db, CancellationToken cancellationToken = default)
        {
            if (changeSet.Count == 0) return;

            // bucket: (rootType, rootId) -> bundled changes for that save
            var buckets = new Dictionary<(Type RootType, NanoId RootId), ObjectChanges>();
            var lookup = BuildParentLookup(changeSet, db);

            foreach (var change in changeSet)
            {
                var entity = change.Entity ?? change.PreviousEntity;
                var clrType = entity?.GetType();
                if (clrType is null) continue;

                if (typeof(IHistory).IsAssignableFrom(clrType))
                {
                    var rootId = entity.Id;
                    var excluded = GetRootExcluded(clrType);
                    var rootChanges = (ObjectChanges)CalculateForRoot(clrType, change.State, change.PreviousEntity, change.Entity);
                    var filtered = rootChanges.FilterKeys(k => !excluded.Contains(k, StringComparer.OrdinalIgnoreCase));
                    GetOrCreate(buckets, clrType, rootId).MergeFrom(filtered);
                    continue;
                }

                if (pathResolver is null) continue;
                if (!pathResolver.TryGet(clrType, out var path)) continue;

                var resolvedRootId = path.ResolveRootId(change.Entity, change.PreviousEntity, lookup);
                var prefix = path.BuildPath(change.Entity, change.PreviousEntity, lookup);
                var childExcluded = GetChildExcluded(clrType, path);
                var childChanges = (ObjectChanges)CalculateForChild(clrType, change.State,
                    change.PreviousEntity, change.Entity, prefix, childExcluded);
                GetOrCreate(buckets, path.RootType, resolvedRootId).MergeFrom(childChanges);
            }

            Activity.Current?.SetTag("history.roots.count", buckets.Count);
            if (buckets.Count == 0) return;
            var written = 0;
            foreach (var ((rootType, rootId), changes) in buckets)
            {
                if (changes.Count == 0) continue;
                using var activity = Tracing.Source.StartActivity($"EfCore.History.Root.{rootType.Name}", ActivityKind.Internal);
                activity?.SetTag("history.root.type", rootType.FullName);
                activity?.SetTag("history.root.id", rootId.Value);
                activity?.SetTag("history.root.paths.count", changes.Count);
                try
                {
                    PersistHistory(rootType, rootId, changes, db);
                    activity?.SetOkStatus();
                    written++;
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }
            Activity.Current?.SetTag("history.rows.written", written);
            await Task.CompletedTask;
        }

        private void PersistHistory(Type rootType, NanoId rootId, ObjectChanges changes, IDb db)
        {
            var historyType = typeof(History<>).MakeGenericType(rootType);
            var idFactory = _newIdFactoryCache.GetOrAdd(historyType, t =>
            {
                var method = typeof(NanoId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m => m.Name == nameof(NanoId.NewId) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    .MakeGenericMethod(t);
                return _ => method.Invoke(null, null)!;
            });

            var history = Activator.CreateInstance(historyType)!;
            historyType.GetProperty(nameof(History.Id))!.SetValue(history, idFactory(rootId));
            historyType.GetProperty(nameof(History.ForeignId))!.SetValue(history, rootId);
            historyType.GetProperty(nameof(History.ChangeSet))!.SetValue(history, changes.ToJson());
            historyType.GetProperty(nameof(History.ModifiedBy))!.SetValue(history,
                (NanoId)(userIdProvider.IsUserInteraction ? userIdProvider.UserId : userIdProvider.FallbackUserId));
            historyType.GetProperty(nameof(History.ModifiedUtc))!.SetValue(history, dateTimeProvider.UtcNow);

            var getMethod = typeof(IDb).GetMethod(nameof(IDb.Get))!.MakeGenericMethod(historyType);
            var dbSet = getMethod.Invoke(db, null)!;
            var add = dbSet.GetType().GetMethod("Add", [historyType])!;
            add.Invoke(dbSet, [history]);
        }

        private static ObjectChanges GetOrCreate(Dictionary<(Type, NanoId), ObjectChanges> buckets, Type rootType, NanoId rootId)
        {
            var key = (rootType, rootId);
            if (!buckets.TryGetValue(key, out var changes))
            {
                changes = new ObjectChanges();
                buckets[key] = changes;
            }
            return changes;
        }

        private string[] GetRootExcluded(Type rootType)
        {
            var attrExcluded = _rootExcludedAttrCache.GetOrAdd(rootType, t => t.GetProperties()
                .Where(p => p.GetCustomAttribute<ExcludeFromHistoryAttribute>() is not null)
                .Select(p => p.Name)
                .ToArray());
            var navs = pathResolver is null
                ? Array.Empty<string>()
                : _rootChildNavCache.GetOrAdd(rootType, t => pathResolver.GetChildren(t)
                    .Select(c => pathResolver.Get(c)!.Links[^1].CollectionOnParent.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            if (navs.Length == 0) return attrExcluded;
            return attrExcluded.Concat(navs).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string[] GetChildExcluded(Type childType, HistoryPathInfo path)
        {
            return _childExcludedCache.GetOrAdd(childType, t => t.GetProperties()
                .Where(p => p.GetCustomAttribute<ExcludeFromHistoryAttribute>() is not null)
                .Select(p => p.Name)
                .Append(path.Links[0].ParentIdProperty.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        private static object CalculateForRoot(Type clrType, ChangeState state, object old, object @new)
        {
            var generic = _calculateRootCache.GetOrAdd(clrType, t => _calculateOpenMethod.MakeGenericMethod(t));
            return generic.Invoke(null, [state, old, @new])!;
        }

        private static object CalculateForChild(Type clrType, ChangeState state, object old, object @new,
            string prefix, string[] excluded)
        {
            var generic = _calculateChildCache.GetOrAdd(clrType, t => _calculateChildOpenMethod.MakeGenericMethod(t));
            return generic.Invoke(null, [state, old, @new, prefix, excluded])!;
        }

        private static Func<Type, NanoId, object> BuildParentLookup(ChangeSet changeSet, IDb db)
        {
            var cache = new Dictionary<(Type, NanoId), object>();
            foreach (var c in changeSet)
            {
                var entity = c.Entity ?? c.PreviousEntity;
                if (entity is null) continue;
                cache[(entity.GetType(), entity.Id)] = entity;
            }
            return (type, id) =>
            {
                if (cache.TryGetValue((type, id), out var value)) return value;
                value = FindFromDb(db, type, id);
                cache[(type, id)] = value;
                return value;
            };
        }

        private static object FindFromDb(IDb db, Type type, NanoId id)
        {
            if (db is null) return null;
            var getMethod = typeof(IDb).GetMethod(nameof(IDb.Get))!.MakeGenericMethod(type);
            var dbSet = getMethod.Invoke(db, null);
            if (dbSet is null) return null;
            var findMethod = dbSet.GetType().GetMethod("Find", new[] { typeof(object[]) });
            return findMethod?.Invoke(dbSet, new object[] { new object[] { id } });
        }
    }
}
