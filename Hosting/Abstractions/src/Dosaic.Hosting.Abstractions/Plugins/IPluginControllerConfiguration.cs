using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Hosting.Abstractions.Plugins
{
    public interface IPluginControllerConfiguration : IPluginActivateable
    {
        void ConfigureControllers(IMvcBuilder controllerBuilder);
    }
}
