using Microsoft.Extensions.DependencyInjection;
using Dosaic.Hosting.Abstractions.Plugins;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity
{
    public sealed class RestResourceEntityPlugin : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<GlobalResponseOptions>();
        }
    }
}
