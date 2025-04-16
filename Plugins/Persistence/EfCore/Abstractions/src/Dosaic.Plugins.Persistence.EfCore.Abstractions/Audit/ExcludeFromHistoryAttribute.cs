namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeFromHistoryAttribute : Attribute;
}