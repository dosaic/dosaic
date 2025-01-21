using Microsoft.AspNetCore.Routing;

namespace Dosaic.Hosting.Abstractions.Plugins
{
    public interface IPluginEndpointsConfiguration : IPluginActivateable
    {
        void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider);
    }
}
