using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis.RateLimiting
{
    /// <summary>What distinguishes one limiter algorithm from another; everything else is shared.</summary>
    internal sealed class RedisRateLimiterDefinition
    {
        public string Script { get; init; }
        public RedisKey Key { get; init; }
        public int PermitLimit { get; init; }
        public int QueueLimit { get; init; }

        /// <summary>Builds ARGV from the requested permits and the lease id (null unless <see cref="Releasable" />).</summary>
        public Func<int, string, RedisValue[]> Arguments { get; init; }

        /// <summary>True when permits are handed back by disposing the lease instead of by time passing.</summary>
        public bool Releasable { get; init; }

        /// <summary>The in-process limiter with the same options, used while Redis cannot be reached.</summary>
        public Func<RateLimiter> CreateFallback { get; init; }
    }

    /// <summary>
    ///     A rate limiter whose budget lives in Redis, shared by every limiter instance with the same key.
    ///     Waiting (<c>QueueLimit &gt; 0</c>) is done by polling: there is no cross-process queue, so
    ///     <see cref="QueueProcessingOrder" /> is not honoured and <c>QueueLimit</c> bounds the waiters of
    ///     this instance only.
    /// </summary>
    internal sealed class RedisRateLimiter : RateLimiter
    {
        internal static readonly TimeSpan DefaultRedisRetryInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _releasableRetryAfterCap = TimeSpan.FromSeconds(1);

        private readonly Func<IDatabase> _database;
        private readonly RedisRateLimiterDefinition _definition;
        private readonly RedisKey[] _keys;
        private readonly Lazy<RateLimiter> _fallback;
        private readonly ILogger _logger;
        private readonly long _redisRetryIntervalInMs;
        private long _skipRedisUntil;
        private int _queued;

        public RedisRateLimiter(Func<IDatabase> database, RedisRateLimiterDefinition definition, ILogger logger,
            TimeSpan? redisRetryInterval = null)
        {
            _database = database;
            _definition = definition;
            _keys = [definition.Key];
            _fallback = new Lazy<RateLimiter>(definition.CreateFallback);
            _logger = logger;
            _redisRetryIntervalInMs = (long)(redisRetryInterval ?? DefaultRedisRetryInterval).TotalMilliseconds;
        }

        public override TimeSpan? IdleDuration => null;

        /// <summary>The budget is shared, so a local snapshot would be wrong — there are no statistics.</summary>
        public override RateLimiterStatistics GetStatistics() => null;

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            EnsurePermitCount(permitCount);
            if (IsRedisSkipped()) return _fallback.Value.AttemptAcquire(permitCount);
            try
            {
                var leaseId = NewLeaseId();
                var result = _database().ScriptEvaluate(_definition.Script, _keys,
                    _definition.Arguments(permitCount, leaseId));
                return ToLease(Parse(result, leaseId), permitCount);
            }
            catch (Exception e) when (IsRedisFailure(e))
            {
                SkipRedis(e);
                return _fallback.Value.AttemptAcquire(permitCount);
            }
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount,
            CancellationToken cancellationToken)
        {
            EnsurePermitCount(permitCount);
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = await TryAcquireAsync(permitCount);
            if (attempt is null) return await _fallback.Value.AcquireAsync(permitCount, cancellationToken);
            if (attempt.Value.Acquired || permitCount == 0 || _definition.QueueLimit == 0)
                return ToLease(attempt.Value, permitCount);
            if (Interlocked.Add(ref _queued, permitCount) > _definition.QueueLimit)
            {
                Interlocked.Add(ref _queued, -permitCount);
                return ToLease(attempt.Value, permitCount);
            }

            try
            {
                for (var round = 0; ; round++)
                {
                    await Task.Delay(GetPollDelay(attempt.Value.RetryAfter, round), cancellationToken);
                    attempt = await TryAcquireAsync(permitCount);
                    if (attempt is null)
                        return await _fallback.Value.AcquireAsync(permitCount, cancellationToken);
                    if (attempt.Value.Acquired) return ToLease(attempt.Value, permitCount);
                }
            }
            finally
            {
                Interlocked.Add(ref _queued, -permitCount);
            }
        }

        /// <summary>Returns null when Redis could not answer and the in-process limiter has to decide.</summary>
        private async Task<Attempt?> TryAcquireAsync(int permitCount)
        {
            if (IsRedisSkipped()) return null;
            try
            {
                var leaseId = NewLeaseId();
                var result = await _database().ScriptEvaluateAsync(_definition.Script, _keys,
                    _definition.Arguments(permitCount, leaseId));
                return Parse(result, leaseId);
            }
            catch (Exception e) when (IsRedisFailure(e))
            {
                SkipRedis(e);
                return null;
            }
        }

        private void EnsurePermitCount(int permitCount)
        {
            if (permitCount > _definition.PermitLimit)
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount,
                    $"{permitCount} permits exceeds the permit limit of {_definition.PermitLimit}.");
        }

        private string NewLeaseId() => _definition.Releasable ? Guid.NewGuid().ToString("N") : null;

        private static Attempt Parse(RedisResult result, string leaseId)
        {
            var values = (RedisResult[])result;
            return new Attempt((long)values[0] == 1, TimeSpan.FromMilliseconds((long)values[1]), leaseId);
        }

        private RedisRateLimitLease ToLease(Attempt attempt, int permitCount)
        {
            if (!attempt.Acquired)
            {
                // nobody knows when a held permit is released, the script can only tell when it would expire
                var retryAfter = _definition.Releasable && attempt.RetryAfter > _releasableRetryAfterCap
                    ? _releasableRetryAfterCap
                    : attempt.RetryAfter;
                return RedisRateLimitLease.Rejected(retryAfter);
            }

            return _definition.Releasable && permitCount > 0
                ? RedisRateLimitLease.Acquired(() => Release(attempt.LeaseId, permitCount))
                : RedisRateLimitLease.Acquired();
        }

        private void Release(string leaseId, int permitCount)
        {
            try
            {
                var members = Enumerable.Range(1, permitCount).Select(i => (RedisValue)$"{leaseId}:{i}").ToArray();
                _database().SortedSetRemove(_definition.Key, members, CommandFlags.FireAndForget);
            }
            catch (Exception e) when (IsRedisFailure(e))
            {
                _logger.LogDebug(e, "Could not release rate limit lease on {RateLimitKey}, it expires on its own",
                    _definition.Key.ToString());
            }
        }

        /// <summary>
        ///     Time based limiters free permits exactly when the script says so. Releasable permits can come
        ///     back at any moment, so those are polled with a short, growing interval instead.
        /// </summary>
        private TimeSpan GetPollDelay(TimeSpan retryAfter, int round)
        {
            var delay = retryAfter.TotalMilliseconds;
            if (_definition.Releasable)
                delay = Math.Min(delay, Math.Min(25d * Math.Pow(2, Math.Min(round, 8)), 500d));
            return TimeSpan.FromMilliseconds(1 + delay + (delay * 0.2 * Random.Shared.NextDouble()));
        }

        private static bool IsRedisFailure(Exception e) => e is RedisException or TimeoutException;

        private bool IsRedisSkipped() => Environment.TickCount64 < Volatile.Read(ref _skipRedisUntil);

        private void SkipRedis(Exception e)
        {
            Volatile.Write(ref _skipRedisUntil, Environment.TickCount64 + _redisRetryIntervalInMs);
            _logger.LogWarning(e,
                "Redis rate limiter {RateLimitKey} is unavailable, limiting in-process for the next {RetryInMs} ms",
                _definition.Key.ToString(), _redisRetryIntervalInMs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _fallback.IsValueCreated) _fallback.Value.Dispose();
            base.Dispose(disposing);
        }

        private readonly record struct Attempt(bool Acquired, TimeSpan RetryAfter, string LeaseId);
    }
}
