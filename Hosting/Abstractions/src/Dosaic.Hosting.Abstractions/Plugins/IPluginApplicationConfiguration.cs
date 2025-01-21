using Microsoft.AspNetCore.Builder;

namespace Dosaic.Hosting.Abstractions.Plugins
{
    public interface IPluginApplicationConfiguration : IPluginActivateable
    {
        void ConfigureApplication(IApplicationBuilder applicationBuilder);
    }
}
