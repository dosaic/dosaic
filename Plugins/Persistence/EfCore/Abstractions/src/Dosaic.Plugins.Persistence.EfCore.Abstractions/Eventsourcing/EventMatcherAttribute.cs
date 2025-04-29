using System.Collections.Immutable;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    [AttributeUsage(AttributeTargets.Property)]
    public class EventMatcherAttribute : Attribute;


    public interface IEventProcessor<TAggregate> where TAggregate : AggregateEvent
    {
        Task ProcessEventsAsync(IDb db, ImmutableArray<TAggregate> events, CancellationToken cancellationToken);
    }

    public interface IEventProjector<T>
    {
        T ProjectEvents(IEnumerable<T> mappedEvents);
    }

    public interface IEventTypeMapperFactory<out T> where T : IEventMapper
    {
        T Resolve(Enum key);
    }

    public interface IEventMapper
    {
        static abstract IDictionary<Enum, Type> Mappers { get; }
    }

    public class EventTypeMapperFactory<T>(IServiceProvider serviceProvider) : IEventTypeMapperFactory<T> where T : IEventMapper
    {
        public T Resolve(Enum key)
        {
            var mapper = T.Mappers[key];
            return (T)serviceProvider.GetRequiredService(mapper);
        }
    }




}
