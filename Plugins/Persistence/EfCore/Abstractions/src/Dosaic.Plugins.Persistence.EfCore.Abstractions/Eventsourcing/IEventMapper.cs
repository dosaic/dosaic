namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public interface IEventMapper
    {
        static abstract IDictionary<Enum, Type> Mappers { get; }
    }
}