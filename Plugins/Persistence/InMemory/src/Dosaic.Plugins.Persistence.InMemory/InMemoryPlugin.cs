using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.InMemory
{
    public class InMemoryPlugin : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(typeof(InMemoryStore));
            serviceCollection.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
            serviceCollection.AddSingleton(typeof(IReadRepository<>), typeof(InMemoryRepository<>));
        }
    }
}
