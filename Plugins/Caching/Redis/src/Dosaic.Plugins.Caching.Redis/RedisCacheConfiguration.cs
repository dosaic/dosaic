using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Caching.Redis
{
    [Configuration("redisCache")]
    public class RedisCacheConfiguration
    {
        public bool UseInMemory { get; set; }
        public string ConnectionString { get; set; } = null!;

        /// <summary>
        ///     Prefix for every key this plugin writes to Redis (cache entries and rate limiter state), so
        ///     several installations can share one Redis. Use the same value when you talk to
        ///     <c>IConnectionMultiplexer</c> yourself. Null (default) means no prefix. Ignored in in-memory mode.
        /// </summary>
        public string InstanceName { get; set; }

        /// <summary>
        ///     How long a permit of a distributed concurrency limiter stays taken once its holder stops
        ///     renewing it, for example because the process crashed. Held permits are renewed three times
        ///     per TTL, so a call may run longer than this without losing its permit.
        /// </summary>
        public int RateLimitLeaseTtlInSeconds { get; set; } = 120;
    }
}
