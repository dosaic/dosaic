using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Plugins.Caching.Redis.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis.Tests.RateLimiting
{
    public class RedisRateLimiterFactoryTests
    {
        private IConnectionMultiplexer _multiplexer;
        private IDatabase _database;
        private RedisCacheConfiguration _configuration;
        private string _script;
        private RedisKey[] _keys;
        private RedisValue[] _arguments;

        [SetUp]
        public void Up()
        {
            _database = Substitute.For<IDatabase>();
            _database.ScriptEvaluate(Arg.Do<string>(x => _script = x), Arg.Do<RedisKey[]>(x => _keys = x),
                    Arg.Do<RedisValue[]>(x => _arguments = x), Arg.Any<CommandFlags>())
                .Returns(RedisResult.Create(new RedisValue[] { 1, 0 }));
            _multiplexer = Substitute.For<IConnectionMultiplexer>();
            _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
            _configuration = new RedisCacheConfiguration { ConnectionString = "redis-host" };
        }

        private RedisRateLimiterFactory Factory() => new(_multiplexer, _configuration, NullLoggerFactory.Instance);

        private static SlidingWindowRateLimiterOptions Sliding() => new()
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(8),
            SegmentsPerWindow = 4
        };

        private static FixedWindowRateLimiterOptions Fixed() => new()
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(2)
        };

        private static TokenBucketRateLimiterOptions Token() => new()
        {
            TokenLimit = 30,
            TokensPerPeriod = 3,
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(1500)
        };

        private static ConcurrencyLimiterOptions Concurrency() => new() { PermitLimit = 40 };

        [Test]
        public void TheSlidingWindowLimiterRunsItsScriptOnItsOwnKey()
        {
            using var lease = Factory().CreateSlidingWindow("client", Sliding()).AttemptAcquire(2);

            lease.IsAcquired.Should().BeTrue();
            _script.Should().Be(RedisRateLimitScripts.SlidingWindow);
            _keys.Should().Equal("ratelimit:client:sliding");
            _arguments.Should().Equal(10, 8000, 4, 2);
        }

        [Test]
        public void TheFixedWindowLimiterRunsItsScriptOnItsOwnKey()
        {
            using var lease = Factory().CreateFixedWindow("client", Fixed()).AttemptAcquire();

            _script.Should().Be(RedisRateLimitScripts.FixedWindow);
            _keys.Should().Equal("ratelimit:client:fixed");
            _arguments.Should().Equal(20, 2000, 1);
        }

        [Test]
        public void TheTokenBucketLimiterRunsItsScriptOnItsOwnKey()
        {
            using var lease = Factory().CreateTokenBucket("client", Token()).AttemptAcquire(3);

            _script.Should().Be(RedisRateLimitScripts.TokenBucket);
            _keys.Should().Equal("ratelimit:client:token");
            _arguments.Should().Equal(30, 3, 1500, 3);
        }

        [Test]
        public void TheConcurrencyLimiterRunsItsScriptWithTheConfiguredLeaseTtl()
        {
            _configuration.RateLimitLeaseTtlInSeconds = 45;

            using var lease = Factory().CreateConcurrency("client", Concurrency()).AttemptAcquire();

            _script.Should().Be(RedisRateLimitScripts.Concurrency);
            _keys.Should().Equal("ratelimit:client:concurrency");
            _arguments.Should().HaveCount(4);
            _arguments.Take(3).Should().Equal(40, 45_000, 1);
            _arguments[3].ToString().Should().NotBeNullOrEmpty();
        }

        [Test]
        public void TheLeaseTtlDefaultsToTwoMinutes()
        {
            new RedisCacheConfiguration().RateLimitLeaseTtlInSeconds.Should().Be(120);
        }

        [Test]
        public void KeysArePrefixedWithTheInstanceName()
        {
            _configuration.InstanceName = "tenant-a:";

            using var lease = Factory().CreateFixedWindow("client", Fixed()).AttemptAcquire();

            _keys.Should().Equal("tenant-a:ratelimit:client:fixed");
        }

        [Test]
        public void RedisErrorsFallBackToAnInProcessLimiterWithTheSameOptions()
        {
            _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>())
                .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "gone"));
            var options = new ConcurrencyLimiterOptions { PermitLimit = 2 };
            var limiters = new[]
            {
                Factory().CreateConcurrency("client", options),
                Factory().CreateFixedWindow("client", new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(1) }),
                Factory().CreateSlidingWindow("client", new SlidingWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 2 }),
                Factory().CreateTokenBucket("client", new TokenBucketRateLimiterOptions { TokenLimit = 2, TokensPerPeriod = 1, ReplenishmentPeriod = TimeSpan.FromMinutes(1) })
            };

            foreach (var limiter in limiters)
            {
                limiter.AttemptAcquire().IsAcquired.Should().BeTrue();
                limiter.AttemptAcquire().IsAcquired.Should().BeTrue();
                limiter.AttemptAcquire().IsAcquired.Should().BeFalse("the fallback has the same limit of 2");
                limiter.Dispose();
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void AKeyIsRequired(string key)
        {
            var factory = Factory();

            factory.Invoking(x => x.CreateSlidingWindow(key, Sliding())).Should().Throw<ArgumentException>();
            factory.Invoking(x => x.CreateFixedWindow(key, Fixed())).Should().Throw<ArgumentException>();
            factory.Invoking(x => x.CreateTokenBucket(key, Token())).Should().Throw<ArgumentException>();
            factory.Invoking(x => x.CreateConcurrency(key, Concurrency())).Should().Throw<ArgumentException>();
        }

        [Test]
        public void OptionsAreRequired()
        {
            var factory = Factory();

            factory.Invoking(x => x.CreateSlidingWindow("client", null)).Should().Throw<ArgumentNullException>();
            factory.Invoking(x => x.CreateFixedWindow("client", null)).Should().Throw<ArgumentNullException>();
            factory.Invoking(x => x.CreateTokenBucket("client", null)).Should().Throw<ArgumentNullException>();
            factory.Invoking(x => x.CreateConcurrency("client", null)).Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void InvalidOptionsAreRejectedWhenTheLimiterIsCreated()
        {
            var factory = Factory();
            var sliding = Sliding();
            sliding.SegmentsPerWindow = 0;
            var fixedWindow = Fixed();
            fixedWindow.Window = TimeSpan.Zero;
            var token = Token();
            token.TokensPerPeriod = 0;
            var concurrency = Concurrency();
            concurrency.PermitLimit = 0;

            factory.Invoking(x => x.CreateSlidingWindow("client", sliding)).Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("SegmentsPerWindow");
            factory.Invoking(x => x.CreateFixedWindow("client", fixedWindow)).Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("Window");
            factory.Invoking(x => x.CreateTokenBucket("client", token)).Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("TokensPerPeriod");
            factory.Invoking(x => x.CreateConcurrency("client", concurrency)).Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("PermitLimit");
        }

        [Test]
        public void ANonPositiveLeaseTtlIsRejected()
        {
            _configuration.RateLimitLeaseTtlInSeconds = 0;

            Factory().Invoking(x => x.CreateConcurrency("client", Concurrency())).Should().Throw<ArgumentException>()
                .Which.Message.Should().Contain("redisCache:RateLimitLeaseTtlInSeconds");
        }
    }
}
