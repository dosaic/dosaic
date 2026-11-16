using Dosaic.Hosting.Abstractions.Plugins;
using Hangfire;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    public interface IHangfireConfigurator : IPluginConfigurator
    {
        public bool IncludesStorage { get; }
        void Configure(IGlobalConfiguration config);
        void ConfigureServer(BackgroundJobServerOptions options);

        /// <summary>
        ///     Changes the options of the dashboard at <c>/hangfire</c> before it is mapped. The plugin has already set
        ///     its defaults: <see cref="DashboardOptions.Authorization" /> holds the filter chosen by
        ///     <c>hangfire:dashboard:auth</c> (host or role). Replace <see cref="DashboardOptions.Authorization" /> and
        ///     <see cref="DashboardOptions.AsyncAuthorization" /> to use your own access rules. The filters run after
        ///     <c>UseAuthentication</c> and <c>UseAuthorization</c>, so they can use <c>HttpContext.User</c>.
        /// </summary>
        void ConfigureDashboard(DashboardOptions options) { }
    }
}
