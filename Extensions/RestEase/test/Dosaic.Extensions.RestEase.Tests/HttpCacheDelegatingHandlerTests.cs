using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class HttpCacheDelegatingHandlerTests
    {
        private WireMockServer _server;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task GetIsServedFromCacheOnSecondCall()
        {
            var first = new SomeResource { Id = Guid.NewGuid(), Name = "a" };
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(new[] { first })));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching(o => o.DefaultTtl = TimeSpan.FromMinutes(1));

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            var r1 = await client.Get(CancellationToken.None);
            var r2 = await client.Get(CancellationToken.None);

            r1[0].Id.Should().Be(first.Id);
            r2[0].Id.Should().Be(first.Id);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        [Test]
        public async Task NonCacheableMethodBypassesCache()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "b" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching();

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Create(new SomeResource(), CancellationToken.None);
            await client.Create(new SomeResource(), CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingPost()).Should().HaveCount(2);
        }

        [Test]
        public async Task ResponseNoStoreIsNotCached()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "ns" };
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess()
                    .WithHeader("Cache-Control", "no-store")
                    .WithBody(JsonSerializer.Serialize(new[] { resource })));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching();

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task DisabledOptionBypassesCache()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching(o => o.Enabled = false);

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CachingOptionsBindFromConfiguration()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Api:BaseAddress"] = _server.Url,
                    ["Api:Caching:Enabled"] = "true",
                    ["Api:Caching:DefaultTtl"] = "00:10:00",
                    ["Api:Caching:KeyPrefix"] = "cfg:",
                    ["Api:Caching:Methods:0"] = "GET",
                    ["Api:Caching:Methods:1"] = "HEAD"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "Api")
                .AddCaching();

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var cache = sp.GetRequiredService<IDistributedCache>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
            var stored = await cache.GetAsync($"cfg:GET {_server.Url}/");
            stored.Should().NotBeNull();
        }

        [Test]
        public async Task CustomKeyBuilderIsUsed()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching(o => o.KeyBuilder = _ => "static-key");

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var cache = sp.GetRequiredService<IDistributedCache>();

            await client.Get(CancellationToken.None);
            var stored = await cache.GetAsync("dosaic:restease:static-key");
            stored.Should().NotBeNull();
        }
    }
}
