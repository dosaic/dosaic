using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public abstract class AggregateEvent : IModel
    {
        public required NanoId Id { get; set; }
        public required string EventData { get; set; }
        public bool IsDeleted { get; set; }
        public required DateTime ValidFrom { get; set; }
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
        public NanoId ModifiedBy { get; set; }
    }

    public abstract class AggregateEvent<TModel> : AggregateEvent where TModel : System.Enum
    {
        public required TModel EventType { get; set; }
    }

    /// <summary>
    /// The aggregate root decorator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AggregateRootAttribute<T> : Attribute where T : AggregateEvent;

    /// <summary>
    /// Indicates that the class is an aggregate child of the specified aggregate root. The NavigationProperty is used to find the next child till a root is found. The root is the one with the AggregateRootAttribute.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="NavigationProperty">Navigation Property</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AggregateChildAttribute<T>(string NavigationProperty) : Attribute where T : AggregateEvent
    {
        public string NavigationProperty { get; } = NavigationProperty;
    }
}
