using System.Diagnostics;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Plugins.Caching.Redis.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Dosaic.Plugins.Caching.Redis.Tests.Integration
{
    /// <summary>
    ///     Runs the Lua scripts against a real Redis. Every "instance" below has its own connection, the way
    ///     two pods would. Marked <see cref="ExplicitAttribute" /> so a plain <c>dotnet test</c> never needs
    ///     Docker — run these with <c>dotnet test --filter TestCategory=Integration</c>.
    /// </summary>
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    [TestFixture("redis:7-alpine")]
    [TestFixture("valkey/valkey:8-alpine")]
    public class RedisRateLimiterIntegrationTests(string image)
    {
        private RedisContainer _redis;
        private readonly List<ConnectionMultiplexer> _connections = [];
        private string _key;

        [OneTimeSetUp]
        public async Task StartRedisAsync()
        {
            _redis = new RedisBuilder(image).Build();
            await _redis.StartAsync();
        }

        [OneTimeTearDown]
        public async Task StopRedisAsync()
        {
            foreach (var connection in _connections) await connection.DisposeAsync();
            if (_redis is not null) await _redis.DisposeAsync();
        }

        [SetUp]
        public void Up() => _key = Guid.NewGuid().ToString("N");

        private RedisRateLimiterFactory NewInstance(string instanceName = "tenant-a:", int leaseTtlInSeconds = 120)
        {
            var connection = ConnectionMultiplexer.Connect(_redis.GetConnectionString());
            _connections.Add(connection);
            return new RedisRateLimiterFactory(connection,
                new RedisCacheConfiguration { InstanceName = instanceName, RateLimitLeaseTtlInSeconds = leaseTtlInSeconds },
                NullLoggerFactory.Instance);
        }

        private static async Task<int> AcquireUntilRejectedAsync(RateLimiter limiter, int attempts)
        {
            var acquired = 0;
            for (var i = 0; i < attempts; i++)
                if ((await limiter.AcquireAsync()).IsAcquired)
                    acquired++;
            return acquired;
        }

        private static TimeSpan RetryAfter(RateLimitLease lease)
        {
            lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
            return retryAfter;
        }

        [Test]
        public async Task TwoInstancesWithTheSameKeyShareOneSlidingWindowBudget()
        {
            var options = new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromSeconds(30),
                SegmentsPerWindow = 3
            };
            var first = NewInstance().CreateSlidingWindow(_key, options);
            var second = NewInstance().CreateSlidingWindow(_key, options);

            (await AcquireUntilRejectedAsync(first, 3)).Should().Be(3);
            (await AcquireUntilRejectedAsync(second, 5)).Should().Be(2, "the first instance already took 3 of 5");
            using var rejected = await first.AcquireAsync();

            rejected.IsAcquired.Should().BeFalse();
            RetryAfter(rejected).Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
        }

        [Test]
        public async Task DifferentInstanceNamesDoNotShareABudget()
        {
            var options = new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromSeconds(30) };
            var tenantA = NewInstance("tenant-a:").CreateFixedWindow(_key, options);
            var tenantB = NewInstance("tenant-b:").CreateFixedWindow(_key, options);

            (await AcquireUntilRejectedAsync(tenantA, 4)).Should().Be(2);
            (await AcquireUntilRejectedAsync(tenantB, 4)).Should().Be(2, "tenant-b has a budget of its own");
        }

        [Test]
        public async Task DifferentKeysDoNotShareABudget()
        {
            var options = new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(30) };
            var factory = NewInstance();

            (await AcquireUntilRejectedAsync(factory.CreateFixedWindow(_key, options), 2)).Should().Be(1);
            (await AcquireUntilRejectedAsync(factory.CreateFixedWindow(_key + "-other", options), 2)).Should().Be(1);
        }

        [Test]
        public async Task AllAlgorithmsOfOneKeyKeepSeparateState()
        {
            var factory = NewInstance();
            var window = TimeSpan.FromSeconds(30);
            var limiters = new[]
            {
                factory.CreateFixedWindow(_key, new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = window }),
                factory.CreateSlidingWindow(_key,
                    new SlidingWindowRateLimiterOptions { PermitLimit = 1, Window = window, SegmentsPerWindow = 2 }),
                factory.CreateTokenBucket(_key,
                    new TokenBucketRateLimiterOptions { TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriod = window }),
                factory.CreateConcurrency(_key, new ConcurrencyLimiterOptions { PermitLimit = 1 })
            };

            foreach (var limiter in limiters)
                (await AcquireUntilRejectedAsync(limiter, 2)).Should().Be(1);
        }

        [Test]
        public async Task TheFixedWindowBudgetComesBackWhenTheWindowIsOver()
        {
            var options = new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMilliseconds(600) };
            var first = NewInstance().CreateFixedWindow(_key, options);
            var second = NewInstance().CreateFixedWindow(_key, options);

            (await AcquireUntilRejectedAsync(first, 3)).Should().Be(2);
            using var rejected = await second.AcquireAsync();
            await Task.Delay(RetryAfter(rejected) + TimeSpan.FromMilliseconds(50));

            (await AcquireUntilRejectedAsync(second, 3)).Should().Be(2);
        }

        [Test]
        public async Task TheSlidingWindowReturnsPermitsSegmentBySegment()
        {
            var segment = TimeSpan.FromSeconds(1);
            var options = new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = segment * 2,
                SegmentsPerWindow = 2
            };
            var limiter = NewInstance().CreateSlidingWindow(_key, options);

            await WaitForTheNextSegmentAsync(segment);
            (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
            await WaitForTheNextSegmentAsync(segment);
            (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();
            using var rejected = await limiter.AcquireAsync();
            rejected.IsAcquired.Should().BeFalse();
            RetryAfter(rejected).Should().BeLessThan(segment,
                "the first permit leaves the window when the next segment starts");
            await Task.Delay(RetryAfter(rejected) + TimeSpan.FromMilliseconds(50));

            (await AcquireUntilRejectedAsync(limiter, 2)).Should().Be(1, "only the first permit has slid out");
        }

        /// <summary>
        ///     Segments are cut by the clock of the Redis server, so a test that depends on which segment a
        ///     permit lands in has to line itself up with that clock.
        /// </summary>
        private async Task WaitForTheNextSegmentAsync(TimeSpan segment)
        {
            var server = _connections[^1].GetServers()[0];
            var now = await server.TimeAsync() - DateTime.UnixEpoch;
            var intoSegment = TimeSpan.FromTicks(now.Ticks % segment.Ticks);
            await Task.Delay(segment - intoSegment + TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public async Task TheTokenBucketIsRefilledPeriodByPeriod()
        {
            var options = new TokenBucketRateLimiterOptions
            {
                TokenLimit = 3,
                TokensPerPeriod = 2,
                ReplenishmentPeriod = TimeSpan.FromMilliseconds(500)
            };
            var first = NewInstance().CreateTokenBucket(_key, options);
            var second = NewInstance().CreateTokenBucket(_key, options);

            (await AcquireUntilRejectedAsync(first, 4)).Should().Be(3);
            using var rejected = await second.AcquireAsync();
            rejected.IsAcquired.Should().BeFalse();
            RetryAfter(rejected).Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(500));
            await Task.Delay(RetryAfter(rejected) + TimeSpan.FromMilliseconds(50));

            (await AcquireUntilRejectedAsync(second, 4)).Should().Be(2, "one period refills two tokens");
        }

        [Test]
        public async Task ConcurrencyPermitsAreSharedAndComeBackOnRelease()
        {
            var options = new ConcurrencyLimiterOptions { PermitLimit = 2 };
            var first = NewInstance().CreateConcurrency(_key, options);
            var second = NewInstance().CreateConcurrency(_key, options);

            var leaseA = await first.AcquireAsync();
            using var leaseB = await second.AcquireAsync();
            using var rejected = await first.AcquireAsync();

            leaseA.IsAcquired.Should().BeTrue();
            leaseB.IsAcquired.Should().BeTrue();
            rejected.IsAcquired.Should().BeFalse();
            leaseA.Dispose();
            await WaitUntilAsync(async () => (await second.AcquireAsync(0)).IsAcquired, "the released permit is free");
            using var leaseC = await second.AcquireAsync();
            leaseC.IsAcquired.Should().BeTrue();
        }

        [Test]
        public async Task ConcurrencyPermitsOfACrashedInstanceExpire()
        {
            var options = new ConcurrencyLimiterOptions { PermitLimit = 1 };
            var crashed = NewInstance(leaseTtlInSeconds: 1).CreateConcurrency(_key, options);
            var survivor = NewInstance(leaseTtlInSeconds: 1).CreateConcurrency(_key, options);

            var lost = await crashed.AcquireAsync();
            lost.IsAcquired.Should().BeTrue();
            (await survivor.AcquireAsync()).IsAcquired.Should().BeFalse();
            // a dead process neither releases nor renews - disposing the limiter stops the renewal the same way
            crashed.Dispose();
            await Task.Delay(1500);

            (await survivor.AcquireAsync()).IsAcquired.Should().BeTrue("the lease was never released but expired");
            GC.KeepAlive(lost);
        }

        [Test]
        public async Task AHeldConcurrencyPermitOutlivesTheLeaseTtl()
        {
            var options = new ConcurrencyLimiterOptions { PermitLimit = 1 };
            var holder = NewInstance(leaseTtlInSeconds: 1).CreateConcurrency(_key, options);
            var other = NewInstance(leaseTtlInSeconds: 1).CreateConcurrency(_key, options);

            var held = await holder.AcquireAsync();
            held.IsAcquired.Should().BeTrue();
            for (var i = 0; i < 6; i++)
            {
                await Task.Delay(500);
                (await other.AcquireAsync()).IsAcquired.Should()
                    .BeFalse($"the permit is still held after {(i + 1) * 500} ms, three times the lease TTL in total");
            }

            held.Dispose();
            await WaitUntilAsync(async () => (await other.AcquireAsync(0)).IsAcquired, "the released permit is free");
        }

        [Test]
        public async Task AWaiterGetsThePermitAnotherInstanceReleases()
        {
            var options = new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 1 };
            var first = NewInstance().CreateConcurrency(_key, options);
            var second = NewInstance().CreateConcurrency(_key, options);
            var held = await first.AcquireAsync();

            var waiting = second.AcquireAsync().AsTask();
            await Task.Delay(200);
            waiting.IsCompleted.Should().BeFalse();
            held.Dispose();

            using var lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
            lease.IsAcquired.Should().BeTrue();
        }

        [Test]
        public async Task AWaiterGetsItsPermitWhenTheWindowRollsOver()
        {
            var options = new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMilliseconds(500),
                QueueLimit = 1
            };
            var first = NewInstance().CreateFixedWindow(_key, options);
            var second = NewInstance().CreateFixedWindow(_key, options);
            (await first.AcquireAsync()).IsAcquired.Should().BeTrue();
            var stopwatch = Stopwatch.StartNew();

            using var lease = await second.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            lease.IsAcquired.Should().BeTrue();
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(300));
        }

        [Test]
        public async Task SeveralPermitsAreTakenAndReleasedTogether()
        {
            var limiter = NewInstance().CreateConcurrency(_key, new ConcurrencyLimiterOptions { PermitLimit = 3 });

            var lease = await limiter.AcquireAsync(2);
            (await limiter.AcquireAsync(2)).IsAcquired.Should().BeFalse();
            lease.Dispose();

            await WaitUntilAsync(async () => (await limiter.AcquireAsync(3)).IsAcquired, "both permits are back");
        }

        [Test]
        public void TheSynchronousPathTalksToRedisToo()
        {
            var options = new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(30) };
            var first = NewInstance().CreateFixedWindow(_key, options);
            var second = NewInstance().CreateFixedWindow(_key, options);

            first.AttemptAcquire().IsAcquired.Should().BeTrue();
            second.AttemptAcquire().IsAcquired.Should().BeFalse();
        }

        [Test]
        public async Task StateExpiresFromRedisOnItsOwn()
        {
            var factory = NewInstance();
            var window = TimeSpan.FromSeconds(30);
            await factory.CreateFixedWindow(_key, new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = window })
                .AcquireAsync();
            await factory.CreateSlidingWindow(_key,
                    new SlidingWindowRateLimiterOptions { PermitLimit = 1, Window = window, SegmentsPerWindow = 2 })
                .AcquireAsync();
            await factory.CreateTokenBucket(_key,
                    new TokenBucketRateLimiterOptions { TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriod = window })
                .AcquireAsync();
            await factory.CreateConcurrency(_key, new ConcurrencyLimiterOptions { PermitLimit = 1 }).AcquireAsync();
            var database = _connections[^1].GetDatabase();

            foreach (var algorithm in new[] { "fixed", "sliding", "token", "concurrency" })
            {
                var ttl = await database.KeyTimeToLiveAsync($"tenant-a:ratelimit:{_key}:{algorithm}");
                ttl.Should().NotBeNull($"the {algorithm} state must not stay in Redis forever");
                ttl!.Value.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(2));
            }
        }

        [Test]
        public async Task AnUnreachableRedisFallsBackToTheInProcessLimiter()
        {
            var connection = await ConnectionMultiplexer.ConnectAsync(
                "localhost:1,abortConnect=false,connectTimeout=200,syncTimeout=200,asyncTimeout=200,connectRetry=0");
            _connections.Add(connection);
            var factory = new RedisRateLimiterFactory(connection, new RedisCacheConfiguration(),
                NullLoggerFactory.Instance);
            var limiter = factory.CreateFixedWindow(_key,
                new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromSeconds(30) });

            (await AcquireUntilRejectedAsync(limiter, 4)).Should().Be(2);
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, string because)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await condition()) return;
                await Task.Delay(25);
            }

            Assert.Fail($"Timed out waiting until {because}.");
        }
    }
}
