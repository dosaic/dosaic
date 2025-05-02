namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public interface IEventProjector<T>
    {
        T ProjectEvents(IEnumerable<T> mappedEvents);
    }
}