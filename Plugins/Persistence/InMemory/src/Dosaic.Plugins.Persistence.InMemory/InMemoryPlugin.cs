using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.InMemory
{
    public class InMemoryPlugin : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(typeof(InMemoryStore));
        }
    }
}
