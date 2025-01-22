using Dosaic.Hosting.Abstractions.Plugins;
using Hangfire;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    public interface IHangfireConfigurator : IPluginConfigurator
    {
        public bool IncludesStorage { get; }
        void Configure(IGlobalConfiguration config);
        void ConfigureServer(BackgroundJobServerOptions options);
    }
}
