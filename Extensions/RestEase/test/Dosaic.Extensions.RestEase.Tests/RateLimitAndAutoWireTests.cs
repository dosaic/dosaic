using System.Net;
using System.Net.Http;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Dosaic.Extensions.RestEase.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class RateLimitAndAutoWireTests
    {
        private WireMockServer _server;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task ConcurrencyLimiterBlocksWhenQueueFull()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]").WithDelay(TimeSpan.FromMilliseconds(200)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddRateLimits(r =>
                {
                    r.Concurrency = new ConcurrencyLimiterConfig { PermitLimit = 1, QueueLimit = 0 };
                });

            await using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            using var http = factory.CreateClient(typeof(ISomeApi).FullName);

            var first = http.GetAsync("/");
            var second = await http.GetAsync("/");
            await first;

            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        [Test]
        public async Task FromConfigurationAutoWiresCachingAndResilienceAndRateLimit()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Api:BaseAddress"] = _server.Url,
                    ["Api:Caching:Enabled"] = "true",
                    ["Api:Caching:DefaultTtl"] = "00:05:00",
                    ["Api:Resilience:Enabled"] = "true",
                    ["Api:Resilience:MaxRetryAttempts"] = "2",
                    ["Api:RateLimits:Enabled"] = "true",
                    ["Api:RateLimits:SlidingWindow:Enabled"] = "true",
                    ["Api:RateLimits:SlidingWindow:PermitLimit"] = "50",
                    ["Api:RateLimits:SlidingWindow:Window"] = "00:00:10",
                    ["Api:RateLimits:SlidingWindow:SegmentsPerWindow"] = "4",
                    ["Api:RateLimits:Concurrency:Enabled"] = "true",
                    ["Api:RateLimits:Concurrency:PermitLimit"] = "10",
                    ["Api:RateLimits:Concurrency:QueueLimit"] = "1024"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "Api");

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
            sp.GetService<IDistributedCache>().Should().NotBeNull();
        }

        [Test]
        public async Task FromConfigurationDoesNotEnableDisabledBlocks()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Api:BaseAddress"] = _server.Url,
                    ["Api:Caching:Enabled"] = "false",
                    ["Api:Resilience:Enabled"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "Api");

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }
    }
}
