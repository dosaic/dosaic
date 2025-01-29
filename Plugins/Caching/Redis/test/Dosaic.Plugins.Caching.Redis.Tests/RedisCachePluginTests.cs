using Dosaic.Hosting.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using HealthChecks.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Caching.Redis.Tests;

public class RedisCachePluginTests
{
    private static readonly RedisCacheConfiguration _configuration = new() { ConnectionString = "localhost" };
    private static readonly RedisCachePlugin _plugin = new(_configuration);

    [Test]
    public void RegistersServices()
    {
        var sc = TestingDefaults.ServiceCollection();
        _plugin.ConfigureServices(sc);
        var sp = sc.BuildServiceProvider();
        sp.GetRequiredService<RedisCacheConfiguration>().Should().NotBeNull().And.BeEquivalentTo(_configuration);
        sp.GetRequiredService<IDistributedCache>().Should().BeAssignableTo<RedisCache>();
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
