using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Hosting.Abstractions.Plugins
{
    public interface IPluginHealthChecksConfiguration : IPluginActivateable
    {
        void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder);
    }
}
