using System.Threading.RateLimiting;

namespace Dosaic.Extensions.Abstractions.RateLimiting
{
    /// <summary>
    ///     Creates rate limiters whose budget is identified by <c>key</c> instead of by the limiter instance.
    ///     Every limiter created for the same key and algorithm draws from the same budget, no matter which
    ///     process created it — provided the registered implementation is backed by a shared store.
    /// </summary>
    public interface IDistributedRateLimiterFactory
    {
        RateLimiter CreateSlidingWindow(string key, SlidingWindowRateLimiterOptions options);
        RateLimiter CreateFixedWindow(string key, FixedWindowRateLimiterOptions options);
        RateLimiter CreateTokenBucket(string key, TokenBucketRateLimiterOptions options);
        RateLimiter CreateConcurrency(string key, ConcurrencyLimiterOptions options);
    }
}
