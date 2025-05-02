using System.Collections.Immutable;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public interface IEventProcessor<TAggregate> where TAggregate : AggregateEvent
    {
        Task ProcessEventsAsync(IDb db, ImmutableArray<TAggregate> events, CancellationToken cancellationToken);
    }
}
