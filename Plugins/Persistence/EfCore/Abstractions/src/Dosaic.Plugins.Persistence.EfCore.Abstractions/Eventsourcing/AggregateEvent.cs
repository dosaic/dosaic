using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
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
}
