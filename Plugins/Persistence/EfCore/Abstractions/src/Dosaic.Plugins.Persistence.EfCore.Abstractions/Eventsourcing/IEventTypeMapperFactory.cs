namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public interface IEventTypeMapperFactory<out T> where T : IEventMapper
    {
        T Resolve(Enum key);
    }
}
