namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Marks an entity as a child whose changes are recorded inside the history stream of a parent entity.
    /// The chain of <see cref="HistoryParentAttribute"/> declarations must terminate at an entity implementing
    /// <see cref="IHistory"/> (the root). Children themselves do not get their own history table.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HistoryParentAttribute : Attribute
    {
        public HistoryParentAttribute(Type parentType, string parentIdProperty)
        {
            ParentType = parentType ?? throw new ArgumentNullException(nameof(parentType));
            ParentIdProperty = parentIdProperty ?? throw new ArgumentNullException(nameof(parentIdProperty));
        }

        /// <summary>
        /// The immediate parent CLR type.
        /// </summary>
        public Type ParentType { get; }

        /// <summary>
        /// Name of the foreign-key property on the decorated child type pointing to <see cref="ParentType"/>.
        /// </summary>
        public string ParentIdProperty { get; }

        /// <summary>
        /// Optional navigation property name on the parent that exposes this child (collection or single).
        /// If omitted, the EF model is queried for a unique navigation from <see cref="ParentType"/> to the
        /// decorated child type. Provide an explicit value when the parent has multiple navigations to the child.
        /// </summary>
        public string Collection { get; set; }
    }
}
