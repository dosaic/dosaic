using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Testing.NUnit.Extensions
{
    public class EfFakeDataSeeder(EfFakeDataSeederConfig config)
    {
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var context = config.Context;
            var model = context.Model;

            config.DiscoverRelationSyncs(model);

            var entityTypes = model.GetEntityTypes()
                .Where(x => !x.IsOwned() && x.GetKeys().Any())
                .Where(x => !config.IgnoredEntityTypes.Contains(x.ClrType))
                .ToList();

            var orderedTypes = TopologicSort(entityTypes);

            foreach (var entityType in orderedTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var clrType = entityType.ClrType;
                var count = config.GetCountForType(clrType);
                if (count <= 0)
                    continue;

                await SeedEntityTypeAsync(entityType, clrType, count, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task SeedEntityTypeAsync(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, Type clrType, int count, CancellationToken cancellationToken)
        {
            var method = typeof(EfFakeDataSeeder)
                .GetMethod(nameof(SeedTypeAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var generic = method.MakeGenericMethod(clrType);
            var task = (Task)generic.Invoke(this, [entityType, count, cancellationToken])!;
            return task;
        }

        private async Task SeedTypeAsync<TEntity>(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, int count, CancellationToken cancellationToken)
            where TEntity : class
        {
            var context = config.Context;
            var set = context.Set<TEntity>();
            var foreignKeys = entityType.GetForeignKeys().ToList();

            var fkKeyPools = new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<object>>();
            var fkExtraPools = new Dictionary<(Microsoft.EntityFrameworkCore.Metadata.IForeignKey Fk, string PrincipalProperty), List<object>>();
            var fkPrincipalCounts = new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<int>>();

            count = await BuildForeignKeyPoolsAsync<TEntity>(foreignKeys, fkKeyPools, fkExtraPools, fkPrincipalCounts, count, cancellationToken).ConfigureAwait(false);

            var producedPerFkPrincipal = fkPrincipalCounts.ToDictionary(
                x => x.Key,
                x => x.Value.Select(_ => 0).ToList());

            var pendingCount = 0;

            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entity = config.FakeData.Fake<TEntity>();

                foreach (var fk in foreignKeys)
                {
                    if (!fkKeyPools.TryGetValue(fk, out var keyList) || keyList.Count == 0)
                        continue;

                    var depProp = fk.Properties.Single().PropertyInfo;
                    if (depProp == null)
                        continue;

                    var index = ChoosePrincipalIndex(fk, keyList.Count, fkPrincipalCounts, producedPerFkPrincipal);
                    if (index < 0)
                        continue;

                    depProp.SetValue(entity, keyList[index]);
                    config.ApplyRelationSync(typeof(TEntity), fk, index, entity, keyList, fkExtraPools);
                }

                await set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                pendingCount++;

                if (pendingCount < config.MaxBatchSize)
                    continue;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                pendingCount = 0;
            }

            if (pendingCount > 0)
                await config.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<int> BuildForeignKeyPoolsAsync<TEntity>(
            IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IForeignKey> foreignKeys,
            IDictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<object>> keyPools,
            IDictionary<(Microsoft.EntityFrameworkCore.Metadata.IForeignKey Fk, string PrincipalProperty), List<object>> extraPools,
            IDictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<int>> principalCounts,
            int currentCount,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            var context = config.Context;
            var count = currentCount;

            foreach (var fk in foreignKeys)
            {
                var relationConfig = config.GetRelationCount(typeof(TEntity), fk.DependentToPrincipal?.Name);

                var principalType = fk.PrincipalEntityType.ClrType;
                var principalSet = GetPrincipalSet(context, principalType);

                var principalKeyProp = fk.PrincipalKey.Properties.Single().PropertyInfo;
                if (principalKeyProp == null)
                    continue;

                var syncsForFk = config.GetSyncsForFk(typeof(TEntity), fk);

                if (syncsForFk.Count == 0)
                {
                    var keys = await LoadPrincipalKeysAsync(principalSet, principalType, principalKeyProp, cancellationToken).ConfigureAwait(false);
                    if (!keys.Any())
                    {
                        if (fk.IsRequired)
                            throw new InvalidOperationException($"No principals seeded for type {principalType.Name} required by {typeof(TEntity).Name}");

                        continue;
                    }

                    keyPools[fk] = keys;
                }
                else
                {
                    var extraNames = syncsForFk
                        .Select(x => x.PrincipalPropertyName)
                        .Distinct()
                        .ToArray();

                    var rows = await LoadPrincipalSliceAsync(principalSet, principalType, principalKeyProp, extraNames, cancellationToken).ConfigureAwait(false);
                    if (!rows.Any())
                    {
                        if (fk.IsRequired)
                            throw new InvalidOperationException($"No principals seeded for type {principalType.Name} required by {typeof(TEntity).Name}");

                        continue;
                    }

                    keyPools[fk] = rows.Select(r => r.Key).ToList();

                    foreach (var sync in syncsForFk)
                    {
                        var index = Array.IndexOf(rows[0].ExtraNames, sync.PrincipalPropertyName);
                        if (index < 0)
                            continue;

                        var extras = rows.Select(r => r.Extras[index]).ToList();
                        extraPools[(fk, sync.PrincipalPropertyName)] = extras;
                    }
                }

                if (relationConfig is null || !keyPools.TryGetValue(fk, out var keyList))
                    continue;
                var perPrincipal = EfFakeDataSeederConfig.BuildPerPrincipalCounts(relationConfig, keyList.Count);
                principalCounts[fk] = perPrincipal;

                var totalForFk = perPrincipal.Sum();
                if (totalForFk > count)
                    count = totalForFk;
            }

            return count;
        }

        private static IQueryable GetPrincipalSet(DbContext context, Type principalType)
        {
            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;
            var generic = setMethod.MakeGenericMethod(principalType);
            return (IQueryable)generic.Invoke(context, null)!;
        }

        private static async Task<List<object>> LoadPrincipalKeysAsync(
            IQueryable principalSet,
            Type principalType,
            System.Reflection.PropertyInfo keyProp,
            CancellationToken cancellationToken)
        {
            var parameter = Expression.Parameter(principalType, "e");
            var body = Expression.Property(parameter, keyProp);
            var lambda = Expression.Lambda(body, parameter);

            var selectMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
                .MakeGenericMethod(principalType, keyProp.PropertyType);

            var projected = (IQueryable)selectMethod.Invoke(null, [principalSet, lambda])!;

            var toListMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
                .MakeGenericMethod(keyProp.PropertyType);

            var toListTask = (Task)toListMethod.Invoke(null, [projected, cancellationToken])!;
            await toListTask.ConfigureAwait(false);
            var resultProp = toListTask.GetType().GetProperty("Result")!;
            return ((IEnumerable)resultProp.GetValue(toListTask)!).Cast<object>().ToList();
        }

        private sealed record PrincipalSlice(object Key, string[] ExtraNames, object[] Extras);

        private static async Task<List<PrincipalSlice>> LoadPrincipalSliceAsync(
            IQueryable principalSet,
            Type principalType,
            System.Reflection.PropertyInfo keyProp,
            string[] extraPropNames,
            CancellationToken cancellationToken)
        {
            var param = Expression.Parameter(principalType, "e");

            var keyAccess = Expression.Convert(Expression.Property(param, keyProp), typeof(object));

            var extraProps = extraPropNames
                .Select(principalType.GetProperty)
                .Where(p => p != null)
                .ToArray();

            var extraFields = extraProps
                .Select(p => Expression.Convert(Expression.Property(param, p!), typeof(object)))
                .OfType<Expression>()
                .ToArray();

            var extrasArrayExpr = Expression.NewArrayInit(typeof(object), extraFields);
            var extraNamesConst = Expression.Constant(extraProps.Select(p => p!.Name).ToArray());

            var ctor = typeof(PrincipalSlice).GetConstructors().Single();
            var newExpr = Expression.New(ctor, keyAccess, extraNamesConst, extrasArrayExpr);

            var lambda = Expression.Lambda(newExpr, param);

            var selectMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
                .MakeGenericMethod(principalType, typeof(PrincipalSlice));

            var projected = (IQueryable)selectMethod.Invoke(null, [principalSet, lambda])!;

            var toListMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(PrincipalSlice));

            var toListTask = (Task)toListMethod.Invoke(null, [projected, cancellationToken])!;
            await toListTask.ConfigureAwait(false);
            var resultProp = toListTask.GetType().GetProperty("Result")!;
            return ((IEnumerable)resultProp.GetValue(toListTask)!).Cast<PrincipalSlice>().ToList();
        }

        private static int ChoosePrincipalIndex(
            Microsoft.EntityFrameworkCore.Metadata.IForeignKey fk,
            int keyCount,
            IReadOnlyDictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<int>> principalCounts,
            IDictionary<Microsoft.EntityFrameworkCore.Metadata.IForeignKey, List<int>> producedPerFkPrincipal)
        {
            if (!principalCounts.TryGetValue(fk, out var perPrincipal) || perPrincipal.Count == 0)
            {
                return keyCount == 0 ? -1 : Random.Shared.Next(keyCount);
            }

            var produced = producedPerFkPrincipal[fk];
            var selectable = new List<int>();
            for (var idx = 0; idx < keyCount; idx++)
            {
                if (produced[idx] < perPrincipal[idx])
                    selectable.Add(idx);
            }

            if (selectable.Count == 0)
                return -1;

            var index = selectable[Random.Shared.Next(selectable.Count)];
            produced[index]++;
            return index;
        }

        private static IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IEntityType> TopologicSort(IReadOnlyCollection<Microsoft.EntityFrameworkCore.Metadata.IEntityType> types)
        {
            var result = new List<Microsoft.EntityFrameworkCore.Metadata.IEntityType>();
            var incoming = new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IEntityType, int>();
            var edges = new Dictionary<Microsoft.EntityFrameworkCore.Metadata.IEntityType, List<Microsoft.EntityFrameworkCore.Metadata.IEntityType>>();

            foreach (var type in types)
            {
                incoming[type] = 0;
                edges[type] = [];
            }

            foreach (var type in types)
            {
                foreach (var fk in type.GetForeignKeys())
                {
                    var principal = fk.PrincipalEntityType;
                    if (!types.Contains(principal) || principal == type) continue;
                    edges[principal].Add(type);
                    incoming[type]++;
                }
            }

            var queue = new Queue<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(incoming.Where(x => x.Value == 0).Select(x => x.Key));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                foreach (var dep in edges[current])
                {
                    incoming[dep]--;
                    if (incoming[dep] == 0)
                        queue.Enqueue(dep);
                }
            }

            foreach (var rem in types.Where(t => !result.Contains(t)))
            {
                result.Add(rem);
            }

            return result;
        }
    }
}
