using Microsoft.Extensions.DependencyInjection;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Persistence.Smb
{
    public class SmbStoragePlugin : IPluginServiceConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ISmbStorage, SmbStorage>();
        }
    }
}
