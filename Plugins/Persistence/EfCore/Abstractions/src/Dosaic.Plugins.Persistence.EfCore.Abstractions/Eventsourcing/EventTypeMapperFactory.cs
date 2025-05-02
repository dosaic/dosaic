using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing
{
    public class EventTypeMapperFactory<T>(IServiceProvider serviceProvider)
        : IEventTypeMapperFactory<T> where T : IEventMapper
    {
        public T Resolve(Enum key)
        {
            var mapper = T.Mappers[key];
            return (T)serviceProvider.GetRequiredService(mapper);
        }
    }
}
