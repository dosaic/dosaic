using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Testing.NUnit.Extensions
{
    public class EfFakeDataSeederConfig
    {
        public required DbContext Context { get; init; }
        public required FakeData FakeData { get; init; }

        private IDictionary<Type, int> TotalEntitiesPerType { get; } = new Dictionary<Type, int>();
        internal ISet<Type> IgnoredEntityTypes { get; } = new HashSet<Type>();
        public int DefaultCountPerEntityType { get; private set; } = 10;
        public int MaxBatchSize { get; private set; } = 200;

        private readonly Dictionary<(Type Dependent, string NavigationName), RelationCountConfig> _relationCounts = new();
        private readonly List<RelationSyncConfig> _relationSyncs = [];

        private EfFakeDataSeederConfig() { }

        public static EfFakeDataSeederConfig For(DbContext context) => For(context, FakeData.Instance);

        public static EfFakeDataSeederConfig For(DbContext context, FakeData fakeData)
        {
            return new EfFakeDataSeederConfig
            {
                Context = context,
                FakeData = fakeData
            };
        }

        public EfFakeDataSeederConfig WithTotalCount<T>(int totalEntities) where T : class
        {
            TotalEntitiesPerType[typeof(T)] = totalEntities;
            return this;
        }

        public EfFakeDataSeederConfig WithDefaultCountPerEntityType(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Default count per entity type must be non negative");
            }

            DefaultCountPerEntityType = count;
            return this;
        }

        public EfFakeDataSeederConfig WithBatchSize(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "BatchSize must be non negative");
            }

            MaxBatchSize = count;
            return this;
        }

        public EfFakeDataSeederConfig WithIgnore<T>() where T : class
        {
            IgnoredEntityTypes.Add(typeof(T));
            return this;
        }

        public EfFakeDataSeederConfig WithRelationCount<TDependent>(Expression<Func<TDependent, object>> navigation, int count)
            where TDependent : class
        {
            return WithRelationCount(navigation, count, count);
        }

        public EfFakeDataSeederConfig WithRelationCount<TDependent>(Expression<Func<TDependent, object>> navigation, int min, int max)
            where TDependent : class
        {
            if (min < 0 || max < min)
            {
                throw new ArgumentOutOfRangeException(nameof(min), "Relation count range is invalid");
            }

            var navName = GetMemberName(navigation);
            var key = (typeof(TDependent), navName);
            _relationCounts[key] = RelationCountConfig.FromRange(min, max);
            return this;
        }

        public EfFakeDataSeederConfig WithRelationCount<TDependent>(Expression<Func<TDependent, object>> navigation, params int[] options)
            where TDependent : class
        {
            if (options is null || options.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Relation count options must not be empty");
            }

            if (options.Any(x => x < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Relation count options must be non negative");
            }

            var navName = GetMemberName(navigation);
            var key = (typeof(TDependent), navName);
            _relationCounts[key] = RelationCountConfig.FromList(options);
            return this;
        }

        internal void DiscoverRelationSyncs(Microsoft.EntityFrameworkCore.Metadata.IModel model)
        {
            foreach (var entityType in model.GetEntityTypes())
            {
                var clr = entityType.ClrType;
                var fks = entityType.GetForeignKeys().ToList();

                foreach (var fkToFirst in fks)
                {
                    var firstPrincipal = fkToFirst.PrincipalEntityType;

                    foreach (var fkToTarget in fks)
                    {
                        if (fkToTarget == fkToFirst)
                            continue;

                        var targetPrincipal = fkToTarget.PrincipalEntityType;

                        var hopFk = firstPrincipal
                            .GetForeignKeys()
                            .FirstOrDefault(x => x.PrincipalEntityType == targetPrincipal);

                        if (hopFk == null)
                            continue;

                        var depKey = fkToTarget.Properties.SingleOrDefault();
                        var hopDepKey = hopFk.Properties.SingleOrDefault();

                        if (depKey?.PropertyInfo == null || hopDepKey?.PropertyInfo == null)
                            continue;

                        var depPropName = depKey.PropertyInfo.Name;
                        var principalPropName = hopDepKey.PropertyInfo.Name;

                        var nav = fkToFirst.DependentToPrincipal;
                        if (nav == null)
                            continue;

                        if (_relationSyncs.Any(x => x.DependentType == clr && x.NavigationName == nav.Name && x.DependentPropertyName == depPropName))
                            continue;

                        _relationSyncs.Add(new RelationSyncConfig(clr, nav.Name, depPropName, principalPropName));
                    }
                }
            }
        }

        internal RelationCountConfig GetRelationCount(Type dependentType, string navigationName)
        {
            return navigationName is null ? null : _relationCounts.GetValueOrDefault((dependentType, navigationName));
        }

        internal IReadOnlyList<RelationSyncConfig> GetSyncsForFk(Type dependentType, Microsoft.EntityFrameworkCore.Metadata.IForeignKey fk)
        {
            return _relationSyncs
                .Where(x => x.DependentType == dependentType && x.NavigationName == fk.DependentToPrincipal?.Name)
                .ToList();
        }

        internal static List<int> BuildPerPrincipalCounts(RelationCountConfig config, int principalCount)
        {
            var list = new List<int>(principalCount);
            for (var i = 0; i < principalCount; i++)
            {
                list.Add(config.GetRandomCountPerPrincipal());
            }

            return list;
        }

        internal void ApplyRelationSync(
            Type dependentType,
            Microsoft.EntityFrameworkCore.Metadata.IForeignKey fk,
            int principalIndex,
            object entity,
            IReadOnlyList<object> principalKeys,
            IReadOnlyDictionary<(Microsoft.EntityFrameworkCore.Metadata.IForeignKey Fk, string PrincipalProperty), List<object>> extraPools)
        {
            foreach (var sync in _relationSyncs)
            {
                if (sync.DependentType != dependentType)
                    continue;

                if (fk.DependentToPrincipal?.Name != sync.NavigationName)
                    continue;

                var targetProp = dependentType.GetProperty(sync.DependentPropertyName);
                if (targetProp == null)
                    continue;

                object value;
                if (extraPools.TryGetValue((fk, sync.PrincipalPropertyName), out var extras))
                {
                    if (principalIndex < 0 || principalIndex >= extras.Count)
                        continue;

                    value = extras[principalIndex];
                }
                else
                {
                    if (principalIndex < 0 || principalIndex >= principalKeys.Count)
                        continue;

                    value = principalKeys[principalIndex];
                }

                targetProp.SetValue(entity, value);
            }
        }

        internal int GetCountForType(Type clrType)
        {
            if (IgnoredEntityTypes.Contains(clrType))
                return 0;

            return TotalEntitiesPerType.TryGetValue(clrType, out var count) ? count : DefaultCountPerEntityType;
        }

        private static string GetMemberName<T, TProp>(Expression<Func<T, TProp>> expr)
        {
            if (expr.Body is MemberExpression member)
            {
                return member.Member.Name;
            }

            if (expr.Body is UnaryExpression { Operand: MemberExpression inner })
            {
                return inner.Member.Name;
            }

            throw new ArgumentException("Navigation expression must be a member access", nameof(expr));
        }
    }

    internal sealed class RelationCountConfig
    {
        private readonly int _min;
        private readonly int _max;
        private readonly int[] _options;

        private RelationCountConfig(int min, int max, int[] options)
        {
            _min = min;
            _max = max;
            _options = options;
        }

        public static RelationCountConfig FromRange(int min, int max) => new(min, max, null);
        public static RelationCountConfig FromList(IEnumerable<int> options) => new(0, 0, options.ToArray());

        public int GetRandomCountPerPrincipal()
        {
            if (_options is { Length: > 0 })
            {
                var index = Random.Shared.Next(_options.Length);
                return _options[index];
            }

            if (_min == _max)
            {
                return _min;
            }

            return Random.Shared.Next(_min, _max + 1);
        }
    }

    internal sealed class RelationSyncConfig(
        Type dependentType,
        string navigationName,
        string dependentPropertyName,
        string principalPropertyName)
    {
        public Type DependentType { get; } = dependentType;
        public string NavigationName { get; } = navigationName;
        public string DependentPropertyName { get; } = dependentPropertyName;
        public string PrincipalPropertyName { get; } = principalPropertyName;
    }
}
