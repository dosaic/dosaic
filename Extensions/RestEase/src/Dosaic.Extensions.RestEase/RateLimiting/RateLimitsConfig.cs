using System.Threading.RateLimiting;

namespace Dosaic.Extensions.RestEase.RateLimiting
{
    public sealed class RateLimitsConfig
    {
        public bool Enabled { get; set; } = true;
        public bool ThrowOnRejection { get; set; }
        public SlidingWindowLimiterConfig SlidingWindow { get; set; }
        public FixedWindowLimiterConfig FixedWindow { get; set; }
        public TokenBucketLimiterConfig TokenBucket { get; set; }
        public ConcurrencyLimiterConfig Concurrency { get; set; }
    }

    public abstract class RateLimiterConfigBase
    {
        public bool Enabled { get; set; } = true;
        public int PermitLimit { get; set; } = 100;
        public int QueueLimit { get; set; }
        public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;
    }

    public sealed class ConcurrencyLimiterConfig : RateLimiterConfigBase
    {
    }

    public sealed class SlidingWindowLimiterConfig : RateLimiterConfigBase
    {
        public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
        public int SegmentsPerWindow { get; set; } = 1;
        public bool AutoReplenishment { get; set; } = true;
    }

    public sealed class FixedWindowLimiterConfig : RateLimiterConfigBase
    {
        public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
        public bool AutoReplenishment { get; set; } = true;
    }

    public sealed class TokenBucketLimiterConfig : RateLimiterConfigBase
    {
        public int TokensPerPeriod { get; set; } = 10;
        public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
        public bool AutoReplenishment { get; set; } = true;
    }
}
