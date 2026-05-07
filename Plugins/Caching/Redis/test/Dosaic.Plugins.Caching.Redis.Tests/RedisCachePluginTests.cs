using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using HealthChecks.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis.Tests;

public class RedisCachePluginTests
{
    private static readonly RedisCacheConfiguration _configuration = new() { ConnectionString = "redis-host" };
    private static readonly RedisCachePlugin _plugin = new(_configuration);

    private static IServiceCollection BuildServiceCollectionWithMux(RedisCachePlugin plugin, out IConnectionMultiplexer mux)
    {
        var sc = TestingDefaults.ServiceCollection();
        plugin.ConfigureServices(sc);
        mux = Substitute.For<IConnectionMultiplexer>();
        var muxDescriptor = sc.Single(d => d.ServiceType == typeof(IConnectionMultiplexer));
        sc.Remove(muxDescriptor);
        sc.AddSingleton(mux);
        return sc;
    }

    [Test]
    public void RegistersServices()
    {
        var sc = BuildServiceCollectionWithMux(_plugin, out var mux);
        var sp = sc.BuildServiceProvider();
        sp.Should().RegisterInstrumentation<StackExchangeRedisInstrumentation>();
        sp.GetRequiredService<RedisCacheConfiguration>().Should().NotBeNull().And.BeEquivalentTo(_configuration);
        sp.GetRequiredService<IDistributedCache>().Should().BeAssignableTo<RedisCache>();
        sp.GetRequiredService<IConnectionMultiplexer>().Should().BeSameAs(mux);
    }

    [Test]
    public async Task RedisCacheOptionsReuseRegisteredMultiplexer()
    {
        var sc = BuildServiceCollectionWithMux(_plugin, out var mux);
        var sp = sc.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        opts.ConnectionMultiplexerFactory.Should().NotBeNull();
        var resolved = await opts.ConnectionMultiplexerFactory!();
        resolved.Should().BeSameAs(mux);
    }

    [Test]
    public void AppendsAbortConnectFalseWhenMissing()
    {
        var plugin = new RedisCachePlugin(new RedisCacheConfiguration { ConnectionString = "redis-host," });
        var sc = TestingDefaults.ServiceCollection();
        plugin.Invoking(p => p.ConfigureServices(sc)).Should().NotThrow();
        sc.Should().Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Test]
    public void KeepsExistingAbortConnectValue()
    {
        var plugin = new RedisCachePlugin(new RedisCacheConfiguration { ConnectionString = "redis-host,abortConnect=true" });
        var sc = TestingDefaults.ServiceCollection();
        plugin.Invoking(p => p.ConfigureServices(sc)).Should().NotThrow();
        sc.Should().Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Test]
    public void RegistersServicesInMemory()
    {
        var sc = TestingDefaults.ServiceCollection();
        var plugin = new RedisCachePlugin(new RedisCacheConfiguration { UseInMemory = true });
        plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();
        sp.GetRequiredService<RedisCacheConfiguration>().Should().NotBeNull().And.BeEquivalentTo(new RedisCacheConfiguration { UseInMemory = true });
        sp.GetRequiredService<IDistributedCache>().Should().BeAssignableTo<MemoryDistributedCache>();
    }

    [Test]
    public void RegistersServicesThrowsOnInvalidConfiguration()
    {
        var sc = TestingDefaults.ServiceCollection();
        var plugin = new RedisCachePlugin(new RedisCacheConfiguration());
        plugin.Invoking(x => x.ConfigureServices(sc))
            .Should()
            .Throw<ArgumentException>()
            .Which.Message.Should().Be("Configuration: redisCache.ConnectionString is required but empty");
    }

    [Test]
    public void RegistersHealthCheck()
    {
        var hcBuilder = Substitute.For<IHealthChecksBuilder>();
        _plugin.ConfigureHealthChecks(hcBuilder);
        var sp = TestingDefaults.ServiceCollection().AddSingleton(_configuration).BuildServiceProvider();
        hcBuilder.Received(1)
            .Add(Arg.Is<HealthCheckRegistration>(h =>
                h.Name == "redis"
                && h.Tags.Contains(HealthCheckTag.Readiness.Value)
                && h.Factory(sp).GetType() == typeof(RedisHealthCheck)));
    }
}
