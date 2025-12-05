using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis;

public class RedisCachePlugin(RedisCacheConfiguration configuration) : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(configuration);
        if (configuration.UseInMemory)
        {
            serviceCollection.AddDistributedMemoryCache();
            return;
        }
        if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
            throw new ArgumentException("Configuration: redisCache.ConnectionString is required but empty");
        serviceCollection.AddStackExchangeRedisCache(opts =>
        {
            opts.Configuration = configuration.ConnectionString;
        });
        serviceCollection.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(configuration.ConnectionString));
        serviceCollection.AddOpenTelemetry().WithTracing(x => x.AddRedisInstrumentation());
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        if (configuration.UseInMemory) return;
        healthChecksBuilder.AddRedis(configuration.ConnectionString, "redis", HealthStatus.Unhealthy, tags: [HealthCheckTag.Readiness.Value]);
    }
}
