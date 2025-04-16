namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    [AttributeUsage(AttributeTargets.Enum)]
    public class DbEnumAttribute(string name, string schema) : Attribute
    {
        public string Name { get; } = name;
        public string Schema { get; } = schema;

        public string DbName => $"{Schema}.{Name}";
    }
}