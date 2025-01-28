namespace Dosaic.Plugins.Mapping.Mapster
{

    /// <summary>
    /// Specifies a mapping constraint
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public sealed class MapFromAttribute<T>(params string[] navigationProperties) : Attribute
    {
        public string[] NavigationProperties { get; } = navigationProperties;
        public Type Source => typeof(T);
    }

}
