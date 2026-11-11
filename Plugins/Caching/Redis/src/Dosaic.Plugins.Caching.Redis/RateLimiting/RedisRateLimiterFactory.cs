using System.Threading.RateLimiting;
using Dosaic.Extensions.Abstractions.RateLimiting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis.RateLimiting
{
    /// <summary>
    ///     Creates limiters that keep their budget in Redis under
    ///     <c>{instanceName}ratelimit:{key}:{algorithm}</c>. The algorithm suffix keeps two different
    ///     limiters of one client (which share the key) from writing to the same Redis key.
    /// </summary>
    internal sealed class RedisRateLimiterFactory : IDistributedRateLimiterFactory
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly RedisCacheConfiguration _configuration;
        private readonly ILogger _logger;

        public RedisRateLimiterFactory(IConnectionMultiplexer multiplexer, RedisCacheConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            _multiplexer = multiplexer;
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<RedisRateLimiterFactory>();
        }

        public RateLimiter CreateSlidingWindow(string key, SlidingWindowRateLimiterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Require(options.PermitLimit > 0, nameof(options.PermitLimit));
            Require(options.QueueLimit >= 0, nameof(options.QueueLimit));
            Require(options.SegmentsPerWindow > 0, nameof(options.SegmentsPerWindow));
            Require(options.Window > TimeSpan.Zero, nameof(options.Window));
            return Create(new RedisRateLimiterDefinition
            {
                Script = RedisRateLimitScripts.SlidingWindow,
                Key = GetKey(key, "sliding"),
                PermitLimit = options.PermitLimit,
                QueueLimit = options.QueueLimit,
                Arguments = (permits, _) =>
                    [options.PermitLimit, ToMilliseconds(options.Window), options.SegmentsPerWindow, permits],
                CreateFallback = () => new SlidingWindowRateLimiter(options)
            });
        }

        public RateLimiter CreateFixedWindow(string key, FixedWindowRateLimiterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Require(options.PermitLimit > 0, nameof(options.PermitLimit));
            Require(options.QueueLimit >= 0, nameof(options.QueueLimit));
            Require(options.Window > TimeSpan.Zero, nameof(options.Window));
            return Create(new RedisRateLimiterDefinition
            {
                Script = RedisRateLimitScripts.FixedWindow,
                Key = GetKey(key, "fixed"),
                PermitLimit = options.PermitLimit,
                QueueLimit = options.QueueLimit,
                Arguments = (permits, _) => [options.PermitLimit, ToMilliseconds(options.Window), permits],
                CreateFallback = () => new FixedWindowRateLimiter(options)
            });
        }

        public RateLimiter CreateTokenBucket(string key, TokenBucketRateLimiterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Require(options.TokenLimit > 0, nameof(options.TokenLimit));
            Require(options.QueueLimit >= 0, nameof(options.QueueLimit));
            Require(options.TokensPerPeriod > 0, nameof(options.TokensPerPeriod));
            Require(options.ReplenishmentPeriod > TimeSpan.Zero, nameof(options.ReplenishmentPeriod));
            return Create(new RedisRateLimiterDefinition
            {
                Script = RedisRateLimitScripts.TokenBucket,
                Key = GetKey(key, "token"),
                PermitLimit = options.TokenLimit,
                QueueLimit = options.QueueLimit,
                Arguments = (permits, _) =>
                [
                    options.TokenLimit, options.TokensPerPeriod, ToMilliseconds(options.ReplenishmentPeriod), permits
                ],
                CreateFallback = () => new TokenBucketRateLimiter(options)
            });
        }

        public RateLimiter CreateConcurrency(string key, ConcurrencyLimiterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Require(options.PermitLimit > 0, nameof(options.PermitLimit));
            Require(options.QueueLimit >= 0, nameof(options.QueueLimit));
            Require(_configuration.RateLimitLeaseTtlInSeconds > 0,
                "redisCache:" + nameof(_configuration.RateLimitLeaseTtlInSeconds));
            var leaseTtl = ToMilliseconds(TimeSpan.FromSeconds(_configuration.RateLimitLeaseTtlInSeconds));
            return Create(new RedisRateLimiterDefinition
            {
                Script = RedisRateLimitScripts.Concurrency,
                Key = GetKey(key, "concurrency"),
                PermitLimit = options.PermitLimit,
                QueueLimit = options.QueueLimit,
                Releasable = true,
                LeaseTtl = TimeSpan.FromMilliseconds(leaseTtl),
                Arguments = (permits, leaseId) => [options.PermitLimit, leaseTtl, permits, leaseId],
                CreateFallback = () => new ConcurrencyLimiter(options)
            });
        }

        private RedisRateLimiter Create(RedisRateLimiterDefinition definition) =>
            new(() => _multiplexer.GetDatabase(), definition, _logger);

        private RedisKey GetKey(string key, string algorithm)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A rate limiter key must be provided.", nameof(key));
            return $"{_configuration.InstanceName}ratelimit:{key}:{algorithm}";
        }

        private static long ToMilliseconds(TimeSpan value) => Math.Max(1, (long)Math.Ceiling(value.TotalMilliseconds));

        private static void Require(bool condition, string optionName)
        {
            if (!condition)
                throw new ArgumentException($"{optionName} has an invalid value for a rate limiter.", optionName);
        }
    }
}
