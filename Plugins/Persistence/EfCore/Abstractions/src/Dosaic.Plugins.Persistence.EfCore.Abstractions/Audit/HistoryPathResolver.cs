using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Walks <see cref="HistoryParentAttribute"/> chains, validates them, and exposes resolved
    /// <see cref="HistoryPathInfo"/> entries keyed by child CLR type.
    /// </summary>
    public sealed class HistoryPathResolver
    {
        private readonly ConcurrentDictionary<Type, HistoryPathInfo> _byChild = new();
        private readonly ConcurrentDictionary<Type, List<Type>> _childrenByRoot = new();

        /// <summary>
        /// Builds a resolver by scanning the supplied entity types.
        /// </summary>
        /// <param name="entityTypes">Candidate types; only those carrying <see cref="HistoryParentAttribute"/> are considered.</param>
        /// <param name="tolerateErrors">When <c>true</c>, malformed chains are silently skipped instead of throwing.
        /// Use this for broad assembly scans; pass <c>false</c> at model-build time to surface configuration errors.</param>
        public static HistoryPathResolver Build(IEnumerable<Type> entityTypes, bool tolerateErrors = false)
        {
            var resolver = new HistoryPathResolver();
            var types = entityTypes.Where(t => t.GetCustomAttribute<HistoryParentAttribute>() is not null).ToArray();
            var resolved = new List<Type>(types.Length);
            foreach (var childType in types)
            {
                HistoryPathInfo info;
                try
                {
                    info = ResolveChain(childType);
                }
                catch (InvalidOperationException) when (tolerateErrors)
                {
                    continue;
                }
                resolver._byChild[childType] = info;
                resolver._childrenByRoot.GetOrAdd(info.RootType, _ => new List<Type>()).Add(childType);
                resolved.Add(childType);
            }
            if (tolerateErrors)
            {
                try { ValidateNoDualRole(resolved); }
                catch (InvalidOperationException) { /* ignored in tolerant mode */ }
            }
            else
            {
                ValidateNoDualRole(resolved);
            }
            return resolver;
        }

        public bool TryGet(Type childType, out HistoryPathInfo info) => _byChild.TryGetValue(childType, out info);

        public HistoryPathInfo Get(Type childType) =>
            _byChild.TryGetValue(childType, out var info) ? info : null;

        public IReadOnlyCollection<Type> GetChildren(Type rootType) =>
            _childrenByRoot.TryGetValue(rootType, out var list) ? list : Array.Empty<Type>();

        public IReadOnlyCollection<Type> AllChildTypes => _byChild.Keys.ToArray();

        private static void ValidateNoDualRole(IEnumerable<Type> types)
        {
            foreach (var t in types)
            {
                if (typeof(IHistory).IsAssignableFrom(t))
                    throw new InvalidOperationException(
                        $"Type '{t.FullName}' has [HistoryParent] and also implements IHistory. An entity must be either a history root or a history child, not both.");
            }
        }

        private static HistoryPathInfo ResolveChain(Type startType)
        {
            var seen = new HashSet<Type> { startType };
            var links = new List<HistoryParentLink>();
            var current = startType;
            while (true)
            {
                var attr = current.GetCustomAttribute<HistoryParentAttribute>()
                    ?? throw new InvalidOperationException(
                        $"Type '{current.FullName}' is missing [HistoryParent] in chain originating from '{startType.FullName}'.");
                var fkProp = current.GetProperty(attr.ParentIdProperty)
                    ?? throw new InvalidOperationException(
                        $"Type '{current.FullName}' does not declare parent-id property '{attr.ParentIdProperty}' configured on [HistoryParent].");
                var collectionNav = ResolveCollectionNav(attr.ParentType, current, attr.Collection);
                var isCollection = IsCollectionType(collectionNav.PropertyType);
                links.Add(new HistoryParentLink
                {
                    ParentType = attr.ParentType,
                    ParentIdProperty = fkProp,
                    CollectionOnParent = collectionNav,
                    IsCollection = isCollection
                });

                if (typeof(IHistory).IsAssignableFrom(attr.ParentType))
                    return new HistoryPathInfo { RootType = attr.ParentType, Links = links };
                if (!seen.Add(attr.ParentType))
                    throw new InvalidOperationException(
                        $"Cyclic [HistoryParent] chain detected starting at '{startType.FullName}'.");
                current = attr.ParentType;
            }
        }

        private static PropertyInfo ResolveCollectionNav(Type parentType, Type childType, string explicitName)
        {
            if (!string.IsNullOrEmpty(explicitName))
            {
                var explicitNav = parentType.GetProperty(explicitName)
                    ?? throw new InvalidOperationException(
                        $"Parent type '{parentType.FullName}' has no property '{explicitName}' (configured via Collection on [HistoryParent] of '{childType.FullName}').");
                if (!IsNavOfType(explicitNav, childType))
                    throw new InvalidOperationException(
                        $"Property '{explicitName}' on '{parentType.FullName}' does not target '{childType.FullName}' (configured via Collection on [HistoryParent] of '{childType.FullName}').");
                return explicitNav;
            }

            var candidates = parentType.GetProperties()
                .Where(p => IsNavOfType(p, childType))
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    $"Could not find a navigation on '{parentType.FullName}' that targets '{childType.FullName}'. Set Collection explicitly on [HistoryParent].");
            if (candidates.Length > 1)
                throw new InvalidOperationException(
                    $"Ambiguous navigation: '{parentType.FullName}' has {candidates.Length} navigations targeting '{childType.FullName}'. Set Collection explicitly on [HistoryParent].");
            return candidates[0];
        }

        private static bool IsNavOfType(PropertyInfo p, Type childType)
        {
            if (p.PropertyType == childType) return true;
            if (!IsCollectionType(p.PropertyType)) return false;
            var elem = GetCollectionElementType(p.PropertyType);
            return elem == childType;
        }

        private static bool IsCollectionType(Type t) =>
            t != typeof(string) && (typeof(IEnumerable).IsAssignableFrom(t)) && t != typeof(byte[]);

        private static Type GetCollectionElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
                return t.GetGenericArguments()[0];
            var ienum = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return ienum?.GetGenericArguments()[0];
        }
    }
}
