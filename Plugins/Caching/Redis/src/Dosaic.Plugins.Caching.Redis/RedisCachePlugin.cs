using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.Caching.StackExchangeRedis;
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
        var safeConnectionString = configuration.ConnectionString.TrimEnd(',');
        if (!safeConnectionString.ToLowerInvariant().Contains("abortconnect="))
            safeConnectionString += ",abortConnect=false";

        serviceCollection.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(safeConnectionString));

        serviceCollection.AddStackExchangeRedisCache(_ => { });
        serviceCollection.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((opts, mux) =>
                opts.ConnectionMultiplexerFactory = () => Task.FromResult(mux));

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
