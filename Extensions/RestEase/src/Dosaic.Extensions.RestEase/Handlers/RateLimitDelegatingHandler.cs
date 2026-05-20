using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Dosaic.Extensions.RestEase.RateLimiting;

namespace Dosaic.Extensions.RestEase.Handlers
{
    public sealed class RateLimitDelegatingHandler : DelegatingHandler
    {
        private readonly RateLimiter _limiter;
        private readonly bool _throwOnRejection;

        public RateLimitDelegatingHandler(RateLimiter limiter, bool throwOnRejection = false)
        {
            _limiter = limiter;
            _throwOnRejection = throwOnRejection;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var lease = await _limiter.AcquireAsync(1, cancellationToken);
            if (lease.IsAcquired)
                return await base.SendAsync(request, cancellationToken);

            if (_throwOnRejection)
                throw new HttpRequestException("Client-side rate limit exceeded");

            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { RequestMessage = request };
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return response;
        }

        public static RateLimiter BuildConcurrency(ConcurrencyLimiterConfig c) => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = c.PermitLimit,
            QueueLimit = c.QueueLimit,
            QueueProcessingOrder = c.QueueProcessingOrder
        });

        public static RateLimiter BuildSlidingWindow(SlidingWindowLimiterConfig c) => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = c.PermitLimit,
            QueueLimit = c.QueueLimit,
            QueueProcessingOrder = c.QueueProcessingOrder,
            Window = c.Window,
            SegmentsPerWindow = c.SegmentsPerWindow,
            AutoReplenishment = c.AutoReplenishment
        });

        public static RateLimiter BuildFixedWindow(FixedWindowLimiterConfig c) => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = c.PermitLimit,
            QueueLimit = c.QueueLimit,
            QueueProcessingOrder = c.QueueProcessingOrder,
            Window = c.Window,
            AutoReplenishment = c.AutoReplenishment
        });

        public static RateLimiter BuildTokenBucket(TokenBucketLimiterConfig c) => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = c.PermitLimit,
            QueueLimit = c.QueueLimit,
            QueueProcessingOrder = c.QueueProcessingOrder,
            TokensPerPeriod = c.TokensPerPeriod,
            ReplenishmentPeriod = c.ReplenishmentPeriod,
            AutoReplenishment = c.AutoReplenishment
        });
    }
}
