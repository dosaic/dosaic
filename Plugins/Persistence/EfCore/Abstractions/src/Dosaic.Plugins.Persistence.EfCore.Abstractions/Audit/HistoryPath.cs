using System.Reflection;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// One hop in a history chain: links a child entity to its immediate parent.
    /// </summary>
    public sealed class HistoryParentLink
    {
        public Type ParentType { get; init; }
        public PropertyInfo ParentIdProperty { get; init; }
        public PropertyInfo CollectionOnParent { get; init; }
        public bool IsCollection { get; init; }
    }

    /// <summary>
    /// Resolved path information for a single child entity type, walking from the child up to the root.
    /// </summary>
    public sealed class HistoryPathInfo
    {
        public Type RootType { get; init; }

        /// <summary>
        /// Chain entries ordered from <em>this child</em> upwards (index 0 = link from child to its immediate parent;
        /// last entry = link from the entity directly under the root to the root).
        /// </summary>
        public IReadOnlyList<HistoryParentLink> Links { get; init; } = Array.Empty<HistoryParentLink>();

        /// <summary>
        /// Returns the root id by walking the FK chain. <paramref name="parentLookup"/> resolves an intermediate
        /// parent instance given its type and id when more than one hop is required.
        /// </summary>
        public NanoId ResolveRootId(object entity, object previous, Func<Type, NanoId, object> parentLookup)
        {
            var current = entity ?? previous
                ?? throw new ArgumentNullException(nameof(entity), "entity and previous are both null");
            NanoId parentId = default!;
            for (var i = 0; i < Links.Count; i++)
            {
                var link = Links[i];
                parentId = (NanoId)link.ParentIdProperty.GetValue(current)!;
                if (i == Links.Count - 1) return parentId;
                current = parentLookup(link.ParentType, parentId) ?? throw new InvalidOperationException(
                    $"Could not resolve parent of type '{link.ParentType.Name}' with id '{parentId}' while walking history chain.");
            }
            return parentId;
        }

        /// <summary>
        /// Builds the dotted path prefix from the root down to the child instance.
        /// </summary>
        public string BuildPath(object entity, object previous, Func<Type, NanoId, object> parentLookup)
        {
            var current = entity ?? previous
                ?? throw new ArgumentNullException(nameof(entity), "entity and previous are both null");
            var currentId = (NanoId)current.GetType().GetProperty(nameof(IModel.Id))!.GetValue(current)!;
            var segments = new List<string>(Links.Count);
            for (var i = 0; i < Links.Count; i++)
            {
                var link = Links[i];
                segments.Add(link.IsCollection
                    ? $"{link.CollectionOnParent.Name}.{currentId}"
                    : link.CollectionOnParent.Name);
                if (i == Links.Count - 1) break;
                var parentId = (NanoId)link.ParentIdProperty.GetValue(current)!;
                current = parentLookup(link.ParentType, parentId) ?? throw new InvalidOperationException(
                    $"Could not resolve parent of type '{link.ParentType.Name}' with id '{parentId}' while building history path.");
                currentId = parentId;
            }
            segments.Reverse();
            return string.Join('.', segments);
        }
    }
}
