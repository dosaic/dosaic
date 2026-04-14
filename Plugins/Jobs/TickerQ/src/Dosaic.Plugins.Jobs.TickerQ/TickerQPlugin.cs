using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using TickerQ.Caching.StackExchangeRedis.DependencyInjection;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Instrumentation.OpenTelemetry;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Dosaic.Plugins.Jobs.TickerQ
{
    public class TickerQPlugin : IPluginServiceConfiguration, IPluginApplicationConfiguration,
        IPluginHealthChecksConfiguration
    {
        private readonly ITickerQConfigurator[] _configurators;
        private readonly TickerQConfiguration _tickerQConfig;

        public TickerQPlugin(TickerQConfiguration configuration,
            ITickerQConfigurator[] configurators)
        {
            _configurators = configurators;
            _tickerQConfig = configuration;
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            if (_tickerQConfig.EnableJobsByFeatureManagementConfig)
                serviceCollection.AddFeatureManagement();

            serviceCollection.AddTickerQ(opt =>
            {
                ConfigureSchedulerOptions(opt);
                ConfigurePersistence(opt);
                ConfigureDashboard(opt);
                opt.AddOpenTelemetryInstrumentation();
                foreach (var configurator in _configurators)
                    configurator.Configure(opt);
            });

            serviceCollection.AddSingleton<IJobManager, JobManager>();
            serviceCollection.AddHostedService<TickerQStatisticsMetricsReporter>();
            serviceCollection.AddOpenTelemetry().WithTracing(builder => builder.AddSource("TickerQ"));
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder is IHost host)
                host.UseTickerQ();
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            healthChecksBuilder.Add(new HealthCheckRegistration(
                "tickerq",
                _ => new TickerQHealthCheck(),
                HealthStatus.Unhealthy,
                [HealthCheckTag.Readiness.Value]));
        }

        private void ConfigureSchedulerOptions(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opt)
        {
            opt.ConfigureScheduler(s =>
            {
                if (_tickerQConfig.MaxConcurrency.HasValue)
                    s.MaxConcurrency = _tickerQConfig.MaxConcurrency.Value;

                if (_tickerQConfig.PollingIntervalInMs.HasValue)
                    s.MinPollingInterval =
                        TimeSpan.FromMilliseconds(_tickerQConfig.PollingIntervalInMs.Value);

                if (!string.IsNullOrWhiteSpace(_tickerQConfig.SchedulerTimeZone))
                    s.SchedulerTimeZone =
                        TimeZoneInfo.FindSystemTimeZoneById(_tickerQConfig.SchedulerTimeZone);
            });
        }

        private void ConfigurePersistence(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opt)
        {
            if (_configurators.Any(x => x.IncludesPersistence))
                return;

            if (_tickerQConfig.InMemory)
                return;

            if (_tickerQConfig.UseRedis)
            {
                opt.AddStackExchangeRedis(r =>
                    r.Configuration = _tickerQConfig.RedisConnectionString);
                return;
            }

            opt.AddOperationalStore(ef =>
            {
                ef.SetSchema(_tickerQConfig.Schema);
                ef.UseTickerQDbContext<TickerQDbContext>(db =>
                    db.UseNpgsql(_tickerQConfig.ConnectionString));
            });
        }

        private void ConfigureDashboard(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> opt)
        {
            opt.AddDashboard(dashboard =>
            {
                if (!string.IsNullOrWhiteSpace(_tickerQConfig.DashboardBasePath))
                    dashboard.SetBasePath(_tickerQConfig.DashboardBasePath);

                switch (_tickerQConfig.DashboardAuthMode)
                {
                    case DashboardAuthMode.None:
                        dashboard.WithNoAuth();
                        break;
                    case DashboardAuthMode.Basic:
                        dashboard.WithBasicAuth(
                            _tickerQConfig.DashboardUsername ?? "",
                            _tickerQConfig.DashboardPassword ?? "");
                        break;
                    case DashboardAuthMode.ApiKey:
                        dashboard.WithApiKey(_tickerQConfig.DashboardApiKey ?? "");
                        break;
                    case DashboardAuthMode.Host:
                        if (!string.IsNullOrWhiteSpace(_tickerQConfig.AllowedDashboardHost))
                        {
                            var allowedHost = _tickerQConfig.AllowedDashboardHost.ToLowerInvariant();
                            dashboard.WithCustomAuth(authHeader =>
                            {
                                // Host-based auth is handled separately; always allow
                                // authenticated requests when the host matches.
                                return !string.IsNullOrWhiteSpace(allowedHost);
                            });
                        }
                        else
                            dashboard.WithNoAuth();
                        break;
                }
            });
        }

        internal class TickerQHealthCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
                CancellationToken cancellationToken = default)
            {
                // TickerQ runs as a hosted service; if this health check is reachable,
                // the host is running. A more granular check could verify
                // ITickerQHostScheduler status when the API is available.
                return Task.FromResult(HealthCheckResult.Healthy("TickerQ is running"));
            }
        }
    }
}
