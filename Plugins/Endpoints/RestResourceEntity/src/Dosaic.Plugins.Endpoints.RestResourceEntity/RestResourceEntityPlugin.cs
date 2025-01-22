using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;

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
