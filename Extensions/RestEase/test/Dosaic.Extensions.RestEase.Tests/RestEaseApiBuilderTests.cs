using System.Net;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Dosaic.Extensions.RestEase.RateLimiting;
using Dosaic.Extensions.RestEase.Resilience;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Polly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class RestEaseApiBuilderTests
    {
        private WireMockServer _server;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task AddResilienceEnablesRetry()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("res-sugar")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(500));
            _server.Given(requestMatcher)
                .InScenario("res-sugar")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddResilience(r =>
                {
                    r.MaxRetryAttempts = 2;
                    r.BaseDelay = TimeSpan.FromMilliseconds(10);
                });

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.Resilience.Should().NotBeNull();
            opts.Resilience!.Enabled.Should().BeTrue();
            opts.Resilience.MaxRetryAttempts.Should().Be(2);

            var client = sp.GetRequiredService<ISomeApi>();
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task AddResilienceWithoutLambdaJustEnablesDefaults()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/")
                .AddResilience();

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.Resilience.Should().NotBeNull();
            opts.Resilience!.Enabled.Should().BeTrue();
            opts.Resilience.MaxRetryAttempts.Should().BeNull();
        }

        [Test]
        public async Task AddCachingEnablesCacheStrategy()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching(c => c.DefaultTtl = TimeSpan.FromMinutes(2));

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.Caching!.Enabled.Should().BeTrue();
            opts.Caching.DefaultTtl.Should().Be(TimeSpan.FromMinutes(2));

            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        [Test]
        public async Task AddCachingWithoutLambdaUsesDefaults()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/")
                .AddCaching();

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.Caching!.Enabled.Should().BeTrue();
            opts.Caching.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Test]
        public async Task AddRateLimitsEnablesLimiter()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]").WithDelay(TimeSpan.FromMilliseconds(200)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddRateLimits(r => r.Concurrency = new ConcurrencyLimiterConfig { Enabled = true, PermitLimit = 1, QueueLimit = 0 });

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.RateLimits!.Enabled.Should().BeTrue();

            var client = sp.GetRequiredService<ISomeApi>();
            var first = client.Get(CancellationToken.None);
            Func<Task> second = () => client.Get(CancellationToken.None);
            await second.Should().ThrowAsync<Exception>();
            await first;
        }

        [Test]
        public async Task AdditionalRetryStatusCodesTriggersRetry()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("add-codes")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(401));
            _server.Given(requestMatcher)
                .InScenario("add-codes")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddResilience(r =>
                {
                    r.MaxRetryAttempts = 1;
                    r.BaseDelay = TimeSpan.FromMilliseconds(10);
                    r.AdditionalRetryStatusCodes.Add(HttpStatusCode.Unauthorized);
                });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task ConfigureRetryHookCalled()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("cfg-retry")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(403));
            _server.Given(requestMatcher)
                .InScenario("cfg-retry")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddResilience(r =>
                {
                    r.MaxRetryAttempts = 1;
                    r.BaseDelay = TimeSpan.FromMilliseconds(10);
                    r.ConfigureRetry = retry =>
                    {
                        var def = retry.ShouldHandle;
                        retry.ShouldHandle = async args =>
                            args.Outcome.Result?.StatusCode == HttpStatusCode.Forbidden
                            || await def(args);
                    };
                });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task AddPollyStacksAlongsideAutoPipeline()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddCaching()
                .AddPolly((pb, _) => pb.AddTimeout(TimeSpan.FromSeconds(5)));

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        [Test]
        public async Task InstanceOverloadCopiesResilienceAndRateLimits()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var options = new RestEaseClientOptions
            {
                BaseAddress = _server.Url,
                Resilience = new ResilienceConfig { Enabled = true, MaxRetryAttempts = 1 },
                RateLimits = new RateLimitsConfig { Enabled = false }
            };

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(options);

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(typeof(ISomeApi).FullName);
            opts.Resilience!.Enabled.Should().BeTrue();
            opts.Resilience.MaxRetryAttempts.Should().Be(1);
            opts.RateLimits.Should().NotBeNull();
        }

        [Test]
        public void IDistributedCacheRegisteredEvenWithoutCachingEnabled()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/");
            using var sp = services.BuildServiceProvider();
            sp.GetService<IDistributedCache>().Should().NotBeNull();
        }
    }
}
