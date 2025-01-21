using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Hosting.Abstractions.Plugins
{
    public interface IPluginServiceConfiguration : IPluginActivateable
    {
        void ConfigureServices(IServiceCollection serviceCollection);
    }
}
