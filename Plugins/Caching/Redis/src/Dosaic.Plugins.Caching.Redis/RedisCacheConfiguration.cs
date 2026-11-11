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
        ///     How long a permit of a distributed concurrency limiter stays taken when it is never released,
        ///     for example because the process holding it crashed. Must be longer than the longest call the
        ///     limiter guards, a permit held longer than this is handed out a second time.
        /// </summary>
        public int RateLimitLeaseTtlInSeconds { get; set; } = 120;
    }
}
