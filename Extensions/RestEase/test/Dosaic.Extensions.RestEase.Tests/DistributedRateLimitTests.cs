using System.Net.Http;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Extensions.Abstractions.RateLimiting;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Dosaic.Extensions.RestEase.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Polly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class DistributedRateLimitTests
    {
        private IDistributedRateLimiterFactory _factory;

        [SetUp]
        public void Setup()
        {
            _factory = Substitute.For<IDistributedRateLimiterFactory>();
            _factory.CreateSlidingWindow(default, default).ReturnsForAnyArgs(_ => NewLimiter());
            _factory.CreateFixedWindow(default, default).ReturnsForAnyArgs(_ => NewLimiter());
            _factory.CreateTokenBucket(default, default).ReturnsForAnyArgs(_ => NewLimiter());
            _factory.CreateConcurrency(default, default).ReturnsForAnyArgs(_ => NewLimiter());
        }

        private static ConcurrencyLimiter NewLimiter() =>
            new(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });

        private static RestEaseClientOptions AllLimiters(bool distributed, string distributedKey = null) => new()
        {
            RateLimits = new RateLimitsConfig
            {
                Distributed = distributed,
                DistributedKey = distributedKey,
                SlidingWindow = new SlidingWindowLimiterConfig
                {
                    PermitLimit = 5,
                    QueueLimit = 2,
                    Window = TimeSpan.FromSeconds(10),
                    SegmentsPerWindow = 4
                },
                FixedWindow = new FixedWindowLimiterConfig { PermitLimit = 6, Window = TimeSpan.FromSeconds(20) },
                TokenBucket = new TokenBucketLimiterConfig
                {
                    PermitLimit = 7,
                    TokensPerPeriod = 3,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(30)
                },
                Concurrency = new ConcurrencyLimiterConfig { PermitLimit = 8, QueueLimit = 9 }
            }
        };

        private void Build(RestEaseClientOptions options, bool registerFactory = true, string clientName = "client")
        {
            var services = new ServiceCollection();
            if (registerFactory) services.AddSingleton(_factory);
            using var sp = services.BuildServiceProvider();
            RestEaseClientBuilder.BuildPipeline(new ResiliencePipelineBuilder<HttpResponseMessage>(), options, sp,
                clientName);
        }

        [Test]
        public void TheFactoryIsNotUsedByDefault()
        {
            new RateLimitsConfig().Distributed.Should().BeFalse();
            new RateLimitsConfig().DistributedKey.Should().BeNull();

            Build(AllLimiters(false));

            _factory.ReceivedCalls().Should().BeEmpty();
        }

        [Test]
        public void LocalLimitersNeedNoFactory()
        {
            this.Invoking(x => x.Build(AllLimiters(false), false)).Should().NotThrow();
        }

        [Test]
        public void DistributedLimitersComeFromTheFactoryKeyedByTheClientName()
        {
            Build(AllLimiters(true));

            _factory.Received(1).CreateSlidingWindow("client", Arg.Is<SlidingWindowRateLimiterOptions>(o =>
                o.PermitLimit == 5 && o.QueueLimit == 2 && o.Window == TimeSpan.FromSeconds(10) &&
                o.SegmentsPerWindow == 4));
            _factory.Received(1).CreateFixedWindow("client", Arg.Is<FixedWindowRateLimiterOptions>(o =>
                o.PermitLimit == 6 && o.Window == TimeSpan.FromSeconds(20)));
            _factory.Received(1).CreateTokenBucket("client", Arg.Is<TokenBucketRateLimiterOptions>(o =>
                o.TokenLimit == 7 && o.TokensPerPeriod == 3 && o.ReplenishmentPeriod == TimeSpan.FromSeconds(30)));
            _factory.Received(1).CreateConcurrency("client", Arg.Is<ConcurrencyLimiterOptions>(o =>
                o.PermitLimit == 8 && o.QueueLimit == 9));
        }

        [Test]
        public void TheDistributedKeyReplacesTheClientName()
        {
            Build(AllLimiters(true, "shared-budget"));

            _factory.Received(1).CreateConcurrency("shared-budget", Arg.Any<ConcurrencyLimiterOptions>());
            _factory.DidNotReceive().CreateConcurrency("client", Arg.Any<ConcurrencyLimiterOptions>());
        }

        [Test]
        public void OnlyEnabledLimitersAreCreated()
        {
            var options = AllLimiters(true);
            options.RateLimits.SlidingWindow.Enabled = false;
            options.RateLimits.FixedWindow = null;

            Build(options);

            _factory.ReceivedCalls().Should().HaveCount(2);
        }

        [Test]
        public void DisabledRateLimitsIgnoreTheDistributedFlag()
        {
            var options = AllLimiters(true);
            options.RateLimits.Enabled = false;

            this.Invoking(x => x.Build(options, false)).Should().NotThrow();
        }

        [Test]
        public void DistributedWithoutARegisteredFactoryThrows()
        {
            this.Invoking(x => x.Build(AllLimiters(true), false)).Should().Throw<InvalidOperationException>()
                .Which.Message.Should().Contain("client").And.Contain(nameof(IDistributedRateLimiterFactory));
        }

        [Test]
        public void DistributedWithoutAnyKeyThrows()
        {
            this.Invoking(x => x.Build(AllLimiters(true), true, null)).Should().Throw<InvalidOperationException>()
                .Which.Message.Should().Contain("distributedKey");
        }

        [Test]
        public async Task ConfiguredClientsLimitThroughTheRegisteredFactory()
        {
            using var server = WireMockServer.Start();
            server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Api:BaseAddress"] = server.Url,
                    ["Api:RateLimits:Distributed"] = "true",
                    ["Api:RateLimits:DistributedKey"] = "partner-api",
                    ["Api:RateLimits:ThrowOnRejection"] = "true",
                    ["Api:RateLimits:FixedWindow:PermitLimit"] = "1",
                    ["Api:RateLimits:FixedWindow:Window"] = "00:01:00"
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IDistributedRateLimiterFactory, InProcessRateLimiterFactory>();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration, "Api");
            services.AddRestEaseApiFromConfiguration<ISomeApi>("second", configuration, "Api");
            await using var sp = services.BuildServiceProvider();

            await sp.GetRequiredService<ISomeApi>().Get(CancellationToken.None);
            var second = sp.GetRequiredService<IRestClientFactory>().Create<ISomeApi>("second");

            await second.Invoking(x => x.Get(CancellationToken.None)).Should().ThrowAsync<Exception>();
            server.LogEntries.Should().HaveCount(1, "both clients share the budget of the same distributed key");
        }

        [Test]
        public async Task ConfiguredClientsFailWhenNoFactoryIsRegistered()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = "http://localhost:1";
                o.RateLimits = new RateLimitsConfig
                {
                    Distributed = true,
                    Concurrency = new ConcurrencyLimiterConfig()
                };
            });
            await using var sp = services.BuildServiceProvider();

            var call = () => sp.GetRequiredService<ISomeApi>().Get(CancellationToken.None);

            (await call.Should().ThrowAsync<InvalidOperationException>())
                .Which.Message.Should().Contain(nameof(IDistributedRateLimiterFactory));
        }
    }
}
