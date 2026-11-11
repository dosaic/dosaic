using System.Threading.RateLimiting;

namespace Dosaic.Plugins.Caching.Redis.RateLimiting
{
    internal sealed class RedisRateLimitLease : RateLimitLease
    {
        private readonly TimeSpan? _retryAfter;
        private Action _release;

        private RedisRateLimitLease(bool isAcquired, TimeSpan? retryAfter, Action release)
        {
            IsAcquired = isAcquired;
            _retryAfter = retryAfter;
            _release = release;
        }

        public static RedisRateLimitLease Acquired(Action release = null) => new(true, null, release);

        public static RedisRateLimitLease Rejected(TimeSpan retryAfter) => new(false, retryAfter, null);

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames =>
            _retryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

        public override bool TryGetMetadata(string metadataName, out object metadata)
        {
            if (_retryAfter.HasValue && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = _retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing) => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
