using System.Net.Http;
using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class ServiceCollectionExtensionsTests
    {
        private WireMockServer _server;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task ResolvesTypedClientThroughIHttpClientFactory()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "abc" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url);

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public async Task BuilderAppliesOAuth2AndCustomHandlerChain()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "abc" };
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "tk", expires_in = 300 }));
            _server.Given(Request.Create().WithPath("/").WithHeader("Authorization", "Bearer tk").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
                {
                    o.BaseAddress = _server.Url;
                    o.DefaultHeaders["X-Tenant"] = "tenant-1";
                })
                .AddOAuth2(a =>
                {
                    a.BaseUrl = _server.Url;
                    a.TokenUrlPath = "/auth/token";
                    a.GrantType = GrantType.ClientCredentials;
                    a.ClientId = "c";
                    a.ClientSecret = "s";
                })
                .AddHandler<TaggingHandler>();

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(Request.Create().WithPath("/").UsingPost()).First();
#pragma warning restore CA1826
            entry.RequestMessage.Headers.Should().ContainKey("X-Tenant");
            entry.RequestMessage.Headers.Should().ContainKey("X-Custom");
        }

        [Test]
        public async Task ResilienceHandlerRetries500()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("std")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(500));
            _server.Given(requestMatcher)
                .InScenario("std")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Resilience = new Dosaic.Extensions.RestEase.Resilience.ResilienceConfig
                {
                    Enabled = true,
                    MaxRetryAttempts = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(10)
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCountGreaterThan(1);
        }

        [Test]
        public async Task ResolvesTypedClientFromInstanceOverload()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "inst" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var options = new RestEaseClientOptions
            {
                BaseAddress = _server.Url,
                UserAgent = "instance-overload/1.0",
                Timeout = TimeSpan.FromSeconds(15)
            };
            options.DefaultHeaders["X-Tenant"] = "tenant-x";

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(options);

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(Request.Create().WithPath("/").UsingPost()).First();
#pragma warning restore CA1826
            entry.RequestMessage.Headers.Should().ContainKey("X-Tenant");
        }

        [Test]
        public async Task BindsOptionsFromConfigurationSection()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "cfg" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["MyApi:BaseAddress"] = _server.Url,
                    ["MyApi:UserAgent"] = "cfg-bind/1.0",
                    ["MyApi:Timeout"] = "00:00:20"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "MyApi");

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public void FromConfigurationThrowsOnEmptySectionKey()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            Action act = () => services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "");
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void InstanceOverloadThrowsOnNullOptions()
        {
            var services = new ServiceCollection();
            Action act = () => services.AddRestEaseApi<ISomeApi>((RestEaseClientOptions)null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void RestClientFactoryServiceResolves()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/");
            using var sp = services.BuildServiceProvider();
            sp.GetService<IRestClientFactory>().Should().NotBeNull();
        }

        private sealed class TaggingHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.TryAddWithoutValidation("X-Custom", "yes");
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
