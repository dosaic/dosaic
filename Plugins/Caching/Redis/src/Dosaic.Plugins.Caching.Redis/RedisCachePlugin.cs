using Dosaic.Extensions.Abstractions.RateLimiting;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Caching.Redis.RateLimiting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
            serviceCollection.TryAddSingleton<IDistributedRateLimiterFactory, InProcessRateLimiterFactory>();
            return;
        }
        if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
            throw new ArgumentException("Configuration: redisCache.ConnectionString is required but empty");
        var safeConnectionString = configuration.ConnectionString.TrimEnd(',');
        if (!safeConnectionString.ToLowerInvariant().Contains("abortconnect="))
            safeConnectionString += ",abortConnect=false";

        serviceCollection.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(safeConnectionString));

        serviceCollection.AddStackExchangeRedisCache(opts => opts.InstanceName = configuration.InstanceName);
        serviceCollection.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((opts, mux) =>
                opts.ConnectionMultiplexerFactory = () => Task.FromResult(mux));

        serviceCollection.TryAddSingleton<IDistributedRateLimiterFactory>(sp =>
            new RedisRateLimiterFactory(sp.GetRequiredService<IConnectionMultiplexer>(), configuration,
                sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));

        serviceCollection.AddOpenTelemetry()
            .WithTracing(t => t
                .AddRedisInstrumentation()
                .ConfigureRedisInstrumentation((sp, instr) =>
                    instr.AddConnection(sp.GetRequiredService<IConnectionMultiplexer>())));
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        if (configuration.UseInMemory) return;
        healthChecksBuilder.AddRedis(configuration.ConnectionString, "redis", HealthStatus.Unhealthy, tags: [HealthCheckTag.Readiness.Value]);
    }
}
