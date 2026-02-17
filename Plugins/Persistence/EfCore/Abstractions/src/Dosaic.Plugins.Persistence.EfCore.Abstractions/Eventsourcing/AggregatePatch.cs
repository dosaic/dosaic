using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Dosaic.Extensions.NanoIds;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public enum PatchOperation
    {
        Add,
        Update,
        Delete
    }

    internal record NavigationSegment(
        Type ParentType,
        Type ChildType,
        string DownwardPropertyName,
        bool IsCollection,
        string FkPropertyName);

    internal record AggregateInfo(
        Type AggregateEventType,
        Type RootEntityType,
        IReadOnlyList<NavigationSegment> Segments);

    public record AggregatePatch(
        NanoId AggregateId,
        string Path,
        PatchOperation Operation,
        string Data,
        NanoId EntityId,
        string EntityType)
    {
        public static AggregatePatch FromJson(string json)
        {
            return json.Deserialize<AggregatePatch>();
        }

        public string ToJson()
        {
            return this.Serialize();
        }
    }

    public static class AggregatePatchExtensions
    {
        private static readonly ConcurrentDictionary<Type, AggregateInfo> InfoCache = new();

        internal static AggregateInfo GetAggregateInfo(Type changeType)
        {
            return InfoCache.GetOrAdd(changeType, BuildAggregateInfo);
        }

        public static bool IsAggregateRoot(this Type type)
        {
            return type.GetCustomAttributes(false).Any(a =>
            {
                var attrType = a.GetType();
                return attrType.IsGenericType && attrType.GetGenericTypeDefinition() == typeof(AggregateRootAttribute<>);
            });
        }

        public static bool IsAggregateChild(this Type type)
        {
            return type.GetCustomAttributes(false).Any(a =>
            {
                var attrType = a.GetType();
                return attrType.IsGenericType && attrType.GetGenericTypeDefinition() == typeof(AggregateChildAttribute<>);
            });
        }

        public static async Task<AggregatePatch> GetAggregateChangesAsync<TChange>(
            this IDb db,
            TChange change,
            PatchOperation operation,
            CancellationToken cancellationToken = default) where TChange : class, IModel
        {
            var info = GetAggregateInfo(typeof(TChange));
            var aggregateId = await ResolveAggregateIdAsync(db, change, info, cancellationToken);
            var path = BuildPath(info);
            var data = await BuildDataAsync(db, change, operation, cancellationToken);

            return new AggregatePatch(
                aggregateId,
                path,
                operation,
                data,
                change.Id,
                typeof(TChange).AssemblyQualifiedName);
        }

        public static void ApplyAggregateChanges<TRoot>(this TRoot root, AggregatePatch patch) where TRoot : class, IModel
        {
            var rootType = typeof(TRoot);
            var hasRootAttr = rootType.GetCustomAttributes(false).Any(a =>
            {
                var type = a.GetType();
                return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AggregateRootAttribute<>);
            });
            if (!hasRootAttr)
                throw new InvalidOperationException($"{rootType.FullName} is not an aggregate root.");

            if (string.IsNullOrEmpty(patch.Path))
            {
                ApplyToRoot(root, patch);
                return;
            }

            ApplyToChild(root, patch);
        }

        private static void ApplyToRoot<TRoot>(TRoot root, AggregatePatch patch) where TRoot : class, IModel
        {
            switch (patch.Operation)
            {
                case PatchOperation.Add:
                    throw new InvalidOperationException("Cannot add the aggregate root to itself.");
                case PatchOperation.Delete:
                    throw new InvalidOperationException("Cannot delete the aggregate root via Apply.");
                case PatchOperation.Update:
                    var changedProps = patch.Data.Deserialize<Dictionary<string, JsonElement>>();
                    SetProperties(root, changedProps);
                    break;
            }
        }

        private static void ApplyToChild<TRoot>(TRoot root, AggregatePatch patch) where TRoot : class, IModel
        {
            var entityType = Type.GetType(patch.EntityType);
            if (entityType == null)
                throw new InvalidOperationException($"Could not resolve type {patch.EntityType}.");

            var info = GetAggregateInfo(entityType);
            var segments = info.Segments;

            switch (patch.Operation)
            {
                case PatchOperation.Update:
                    {
                        var target = FindEntityById(root, patch.EntityId, segments, 0);
                        if (target == null)
                            throw new InvalidOperationException($"Entity with Id {patch.EntityId} not found.");
                        var changedProps = patch.Data.Deserialize<Dictionary<string, JsonElement>>();
                        SetProperties(target, changedProps);
                        break;
                    }
                case PatchOperation.Delete:
                    {
                        DeleteEntityFromTree(root, patch.EntityId, segments, 0);
                        break;
                    }
                case PatchOperation.Add:
                    {
                        AddEntityToTree(root, patch, entityType, info);
                        break;
                    }
            }
        }

        private static object FindEntityById(object current, NanoId entityId, IReadOnlyList<NavigationSegment> segments, int depth, int maxDepth = -1)
        {
            var effectiveMax = maxDepth < 0 ? segments.Count : maxDepth;
            if (depth >= effectiveMax) return null;

            var seg = segments[depth];
            var prop = current.GetType().GetProperty(seg.DownwardPropertyName);
            if (prop == null) return null;
            var value = prop.GetValue(current);
            if (value == null) return null;

            if (depth == effectiveMax - 1)
            {
                if (seg.IsCollection)
                    return FindInCollectionById(value, entityId);

                var idProp = value.GetType().GetProperty(nameof(IModel.Id));
                return idProp != null && (NanoId)idProp.GetValue(value) == entityId ? value : null;
            }

            if (seg.IsCollection && value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    var result = FindEntityById(item, entityId, segments, depth + 1, effectiveMax);
                    if (result != null) return result;
                }
            }
            else
            {
                return FindEntityById(value, entityId, segments, depth + 1, effectiveMax);
            }

            return null;
        }

        private static bool DeleteEntityFromTree(object current, NanoId entityId, IReadOnlyList<NavigationSegment> segments, int depth)
        {
            if (depth >= segments.Count) return false;

            var seg = segments[depth];
            var prop = current.GetType().GetProperty(seg.DownwardPropertyName);
            if (prop == null) return false;
            var value = prop.GetValue(current);
            if (value == null) return false;

            if (depth == segments.Count - 1)
            {
                if (seg.IsCollection)
                {
                    var target = FindInCollectionById(value, entityId);
                    var removeMethod = value.GetType().GetMethod("Remove");
                    removeMethod?.Invoke(value, [target]);
                    return true;
                }

                prop.SetValue(current, null);
                return true;
            }

            if (seg.IsCollection && value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (DeleteEntityFromTree(item, entityId, segments, depth + 1))
                        return true;
                }
            }
            else
            {
                return DeleteEntityFromTree(value, entityId, segments, depth + 1);
            }

            return false;
        }

        private static void AddEntityToTree(object root, AggregatePatch patch, Type entityType, AggregateInfo info)
        {
            var entity = patch.Data.Deserialize(entityType);
            var segments = info.Segments;
            var lastSeg = segments[^1];

            object parent;
            if (segments.Count == 1)
            {
                parent = root;
            }
            else
            {
                var fkProp = entityType.GetProperty(lastSeg.FkPropertyName);
                if (fkProp == null)
                    throw new InvalidOperationException($"FK property {lastSeg.FkPropertyName} not found on {entityType.FullName}.");
                var parentId = (NanoId)fkProp.GetValue(entity);
                parent = FindEntityById(root, parentId, segments, 0, segments.Count - 1);
                if (parent == null)
                    throw new InvalidOperationException($"Parent entity with Id {parentId} not found.");
            }

            var prop = parent.GetType().GetProperty(lastSeg.DownwardPropertyName);
            if (prop == null)
                throw new InvalidOperationException($"Property {lastSeg.DownwardPropertyName} not found.");

            if (lastSeg.IsCollection)
            {
                var collection = prop.GetValue(parent);
                if (collection == null)
                {
                    collection = Activator.CreateInstance(typeof(List<>).MakeGenericType(lastSeg.ChildType));
                    prop.SetValue(parent, collection);
                }
                var addMethod = collection.GetType().GetMethod("Add");
                addMethod?.Invoke(collection, [entity]);
            }
            else
            {
                prop.SetValue(parent, entity);
            }
        }

        private static object FindInCollectionById(object collection, NanoId entityId)
        {
            if (collection is not IEnumerable enumerable)
                throw new InvalidOperationException("Expected a collection.");

            foreach (var item in enumerable)
            {
                var idProp = item.GetType().GetProperty(nameof(IModel.Id));
                if (idProp == null) continue;
                var id = (NanoId)idProp.GetValue(item);
                if (id == entityId)
                    return item;
            }
            throw new InvalidOperationException($"Entity with Id {entityId} not found in collection.");
        }

        private static void SetProperties(object target, Dictionary<string, JsonElement> changedProps)
        {
            if (changedProps == null) return;
            var targetType = target.GetType();
            foreach (var (key, value) in changedProps)
            {
                var prop = targetType.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;
                if (prop.PropertyType.Implements(typeof(IModel))) continue;
                if (prop.PropertyType.IsEnumerable() && prop.PropertyType != typeof(string)) continue;

                var deserialized = value.GetRawText().Deserialize(prop.PropertyType);
                prop.SetValue(target, deserialized);
            }
        }

        private static async Task<NanoId> ResolveAggregateIdAsync<TChange>(
            IDb db,
            TChange change,
            AggregateInfo info,
            CancellationToken cancellationToken) where TChange : class, IModel
        {
            if (info.Segments.Count == 0)
                return change.Id;

            var lastSegment = info.Segments[^1];
            var fkProp = typeof(TChange).GetProperty(lastSegment.FkPropertyName);
            if (fkProp == null)
                throw new InvalidOperationException($"FK property {lastSegment.FkPropertyName} not found on {typeof(TChange).FullName}.");

            var parentId = (NanoId)fkProp.GetValue(change);

            if (info.Segments.Count == 1)
                return parentId;

            return await ResolveAggregateIdViaDbAsync(db, parentId, info, cancellationToken);
        }

        private static async Task<NanoId> ResolveAggregateIdViaDbAsync(
            IDb db,
            NanoId parentId,
            AggregateInfo info,
            CancellationToken cancellationToken)
        {
            var currentId = parentId;

            for (var i = info.Segments.Count - 2; i >= 0; i--)
            {
                var seg = info.Segments[i];
                var method = typeof(AggregatePatchExtensions)
                    .GetMethod(nameof(LoadFkAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(seg.ChildType);

                currentId = await (Task<NanoId>)method.Invoke(null, [db, currentId, seg.FkPropertyName, cancellationToken])!;
            }

            return currentId;
        }

        private static async Task<NanoId> LoadFkAsync<T>(
            IDb db,
            NanoId entityId,
            string fkPropertyName,
            CancellationToken cancellationToken) where T : class, IModel
        {
            var param = Expression.Parameter(typeof(T), "x");
            var fkProp = typeof(T).GetProperty(fkPropertyName)!;
            var body = Expression.Property(param, fkProp);
            var lambda = Expression.Lambda<Func<T, NanoId>>(body, param);

            return await db.GetQuery<T>()
                .Where(x => x.Id == entityId)
                .Select(lambda)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static async Task<string> BuildDataAsync<TChange>(
            IDb db,
            TChange change,
            PatchOperation operation,
            CancellationToken cancellationToken) where TChange : class, IModel
        {
            switch (operation)
            {
                case PatchOperation.Delete:
                    return null;

                case PatchOperation.Add:
                    var addProps = GetSerializableProperties(typeof(TChange));
                    var addDict = new Dictionary<string, object>();
                    foreach (var prop in addProps)
                    {
                        var val = prop.GetValue(change);
                        if (val != null)
                            addDict[prop.Name] = val;
                    }
                    return addDict.Serialize();

                case PatchOperation.Update:
                    var current = await db.GetQuery<TChange>()
                        .FirstOrDefaultAsync(x => x.Id == change.Id, cancellationToken);
                    if (current == null)
                        throw new InvalidOperationException($"Entity {typeof(TChange).Name} with Id {change.Id} not found in database.");

                    var updateProps = GetSerializableProperties(typeof(TChange));
                    var diffDict = new Dictionary<string, object>();
                    foreach (var prop in updateProps)
                    {
                        var oldVal = prop.GetValue(current);
                        var newVal = prop.GetValue(change);
                        if (!Equals(oldVal, newVal))
                            diffDict[prop.Name] = newVal;
                    }
                    return diffDict.Serialize();

                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => !p.PropertyType.Implements(typeof(IModel)))
                .Where(p => !p.PropertyType.IsEnumerable() || p.PropertyType == typeof(string));
        }

        private static string BuildPath(AggregateInfo info)
        {
            if (info.Segments.Count == 0)
                return null;

            return string.Join(".",
                info.Segments.Select(s => s.DownwardPropertyName));
        }

        internal static AggregateInfo BuildAggregateInfo(Type changeType)
        {
            var segments = new List<NavigationSegment>();
            var (aggregateEventType, rootEntityType) = WalkToRoot(changeType, segments);
            segments.Reverse();
            return new AggregateInfo(aggregateEventType, rootEntityType, segments);
        }

        private static (Type AggregateEventType, Type RootEntityType) WalkToRoot(
            Type currentType,
            List<NavigationSegment> segments)
        {
            var attributes = currentType.GetCustomAttributes(false);
            var rootAttr = attributes.FirstOrDefault(a =>
            {
                var type = a.GetType();
                return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AggregateRootAttribute<>);
            });
            if (rootAttr != null)
                return (rootAttr.GetType().GetGenericArguments()[0], currentType);

            var childAttr = attributes.FirstOrDefault(a =>
            {
                var type = a.GetType();
                return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AggregateChildAttribute<>);
            });
            if (childAttr == null)
                throw new InvalidOperationException($"{currentType.FullName} must have AggregateRootAttribute or AggregateChildAttribute.");

            var navPropertyName = (string)childAttr.GetType().GetProperty("NavigationProperty")?.GetValue(childAttr);
            var navProperty = currentType.GetProperty(navPropertyName);
            if (navProperty == null)
                throw new InvalidOperationException($"Navigation property {navPropertyName} not found on {currentType.FullName}.");

            var parentType = navProperty.PropertyType;
            var fkPropertyName = navPropertyName + "Id";

            var downwardProp = parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p =>
                {
                    if (p.PropertyType == currentType)
                        return true;
                    if (p.PropertyType.IsGenericType)
                    {
                        var elementType = p.PropertyType.GetGenericArguments().FirstOrDefault();
                        return elementType == currentType;
                    }
                    return false;
                });

            if (downwardProp == null)
                throw new InvalidOperationException($"No property on {parentType.FullName} references {currentType.FullName}.");

            var isCollection = downwardProp.PropertyType.IsGenericType &&
                (downwardProp.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                 downwardProp.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) ||
                 downwardProp.PropertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                 downwardProp.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            segments.Add(new NavigationSegment(
                parentType,
                currentType,
                downwardProp.Name,
                isCollection,
                fkPropertyName));

            return WalkToRoot(parentType, segments);
        }
    }
}
