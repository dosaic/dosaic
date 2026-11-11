using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Dosaic.Extensions.Abstractions.RateLimiting
{
    /// <summary>
    ///     <see cref="IDistributedRateLimiterFactory" /> without a shared store: the budget of a key is shared
    ///     inside this process only. The first options seen for a key and algorithm define its budget.
    ///     Meant for local development, tests and single instance deployments.
    /// </summary>
    public sealed class InProcessRateLimiterFactory : IDistributedRateLimiterFactory, IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<RateLimiter>> _limiters = new();

        public RateLimiter CreateSlidingWindow(string key, SlidingWindowRateLimiterOptions options) =>
            GetOrCreate("sliding", key, options, () => new SlidingWindowRateLimiter(options));

        public RateLimiter CreateFixedWindow(string key, FixedWindowRateLimiterOptions options) =>
            GetOrCreate("fixed", key, options, () => new FixedWindowRateLimiter(options));

        public RateLimiter CreateTokenBucket(string key, TokenBucketRateLimiterOptions options) =>
            GetOrCreate("token", key, options, () => new TokenBucketRateLimiter(options));

        public RateLimiter CreateConcurrency(string key, ConcurrencyLimiterOptions options) =>
            GetOrCreate("concurrency", key, options, () => new ConcurrencyLimiter(options));

        private SharedRateLimiter GetOrCreate(string kind, string key, object options, Func<RateLimiter> create)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A rate limiter key must be provided.", nameof(key));
            ArgumentNullException.ThrowIfNull(options);
            var name = $"{kind}:{key}";
            var limiter = _limiters.GetOrAdd(name, _ => new Lazy<RateLimiter>(create));
            try
            {
                return new SharedRateLimiter(limiter.Value);
            }
            catch
            {
                // invalid options must not poison the key for a later, valid attempt
                _limiters.TryRemove(new KeyValuePair<string, Lazy<RateLimiter>>(name, limiter));
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var limiter in _limiters.Values.Where(x => x.IsValueCreated))
                limiter.Value.Dispose();
            _limiters.Clear();
        }

        /// <summary>
        ///     Hands out the shared limiter without handing out its lifetime: disposing one handle must not
        ///     take the budget away from everybody else using the same key.
        /// </summary>
        private sealed class SharedRateLimiter(RateLimiter inner) : RateLimiter
        {
            public override TimeSpan? IdleDuration => inner.IdleDuration;

            public override RateLimiterStatistics GetStatistics() => inner.GetStatistics();

            protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
                inner.AttemptAcquire(permitCount);

            protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount,
                CancellationToken cancellationToken) => inner.AcquireAsync(permitCount, cancellationToken);
        }
    }
}
