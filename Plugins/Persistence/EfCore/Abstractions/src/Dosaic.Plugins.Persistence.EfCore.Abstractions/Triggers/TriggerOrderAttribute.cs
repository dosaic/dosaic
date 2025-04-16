namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    /// <summary>
    /// Allows to specify the order of the trigger in the database.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TriggerOrderAttribute : Attribute
    {
        public int Order { get; init; }
    }
}