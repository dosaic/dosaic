using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Mapping.Mapster
{
    public class MapsterPlugin(IImplementationResolver implementationResolver) : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            MapsterInitializer.InitMapster(implementationResolver.FindAssemblies().ToArray());
        }
    }
}
