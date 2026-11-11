using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Extensions.Abstractions.RateLimiting;
using NUnit.Framework;

namespace Dosaic.Extensions.Abstractions.Tests.RateLimiting
{
    [TestFixture]
    public class InProcessRateLimiterFactoryTests
    {
        private InProcessRateLimiterFactory _factory;

        [SetUp]
        public void Up() => _factory = new InProcessRateLimiterFactory();

        [TearDown]
        public void Down() => _factory.Dispose();

        private static ConcurrencyLimiterOptions Concurrency(int permitLimit = 1) =>
            new() { PermitLimit = permitLimit, QueueLimit = 0 };

        [Test]
        public void LimitersWithTheSameKeyShareOneBudget()
        {
            var first = _factory.CreateConcurrency("client", Concurrency());
            var second = _factory.CreateConcurrency("client", Concurrency());

            using var lease = first.AttemptAcquire();

            lease.IsAcquired.Should().BeTrue();
            second.AttemptAcquire().IsAcquired.Should().BeFalse();
        }

        [Test]
        public void LimitersWithDifferentKeysDoNotShare()
        {
            var first = _factory.CreateConcurrency("a", Concurrency());
            var second = _factory.CreateConcurrency("b", Concurrency());

            using var lease = first.AttemptAcquire();

            second.AttemptAcquire().IsAcquired.Should().BeTrue();
        }

        [Test]
        public void DifferentAlgorithmsWithTheSameKeyDoNotCollide()
        {
            var window = TimeSpan.FromMinutes(1);
            var limiters = new[]
            {
                _factory.CreateConcurrency("client", Concurrency()),
                _factory.CreateFixedWindow("client",
                    new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = window }),
                _factory.CreateSlidingWindow("client",
                    new SlidingWindowRateLimiterOptions { PermitLimit = 1, Window = window, SegmentsPerWindow = 2 }),
                _factory.CreateTokenBucket("client",
                    new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriod = window
                    })
            };

            foreach (var limiter in limiters)
            {
                limiter.AttemptAcquire().IsAcquired.Should().BeTrue();
                limiter.AttemptAcquire().IsAcquired.Should().BeFalse();
            }
        }

        [Test]
        public void DisposingOneHandleKeepsTheBudgetAliveForTheOthers()
        {
            var first = _factory.CreateConcurrency("client", Concurrency());
            var second = _factory.CreateConcurrency("client", Concurrency());

            first.Dispose();

            second.AttemptAcquire().IsAcquired.Should().BeTrue();
        }

        [Test]
        public async Task HandlesForwardWaitingAndStatistics()
        {
            var limiter = _factory.CreateConcurrency("client", new ConcurrencyLimiterOptions
            {
                PermitLimit = 1,
                QueueLimit = 1
            });
            var lease = await limiter.AcquireAsync();
            var waiting = limiter.AcquireAsync().AsTask();

            limiter.GetStatistics()!.CurrentQueuedCount.Should().Be(1);
            limiter.IdleDuration.Should().BeNull();
            lease.Dispose();

            (await waiting).IsAcquired.Should().BeTrue();
        }

        [Test]
        public void DisposingTheFactoryDisposesItsLimiters()
        {
            var limiter = _factory.CreateConcurrency("client", Concurrency());
            _factory.CreateFixedWindow("never-used",
                new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromSeconds(1) });

            _factory.Dispose();

            limiter.Invoking(x => x.AttemptAcquire()).Should().Throw<ObjectDisposedException>();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RejectsAMissingKey(string key)
        {
            _factory.Invoking(x => x.CreateConcurrency(key, Concurrency())).Should().Throw<ArgumentException>();
        }

        [Test]
        public void InvalidOptionsDoNotPoisonTheKey()
        {
            _factory.Invoking(x => x.CreateConcurrency("client", Concurrency(0))).Should().Throw<ArgumentException>();

            _factory.CreateConcurrency("client", Concurrency()).AttemptAcquire().IsAcquired.Should().BeTrue();
        }

        [Test]
        public void RejectsMissingOptions()
        {
            _factory.Invoking(x => x.CreateConcurrency("client", null)).Should().Throw<ArgumentNullException>();
        }
    }
}
