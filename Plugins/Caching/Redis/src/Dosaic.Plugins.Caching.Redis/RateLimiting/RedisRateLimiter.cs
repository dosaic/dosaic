using System.Collections.Concurrent;
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

        /// <summary>
        ///     How long a <see cref="Releasable" /> permit stays taken in Redis without being renewed. Held
        ///     leases are renewed three times per TTL. Null means leases are never renewed.
        /// </summary>
        public TimeSpan? LeaseTtl { get; init; }

        /// <summary>The in-process limiter with the same options, used while Redis cannot be reached.</summary>
        public Func<RateLimiter> CreateFallback { get; init; }
    }

    /// <summary>
    ///     A rate limiter whose budget lives in Redis, shared by every limiter instance with the same key.
    ///     Waiting (<c>QueueLimit &gt; 0</c>) is done by polling: there is no cross-process queue, so
    ///     <see cref="QueueProcessingOrder" /> is not honoured and <c>QueueLimit</c> bounds the waiters of
    ///     this instance only.
    ///     <para>
    ///         A Redis problem never fails or rejects a call: the call is decided by the in-process limiter
    ///         with the same options. A concurrency lease the script may still have taken is handed back.
    ///         Redis keeps being asked for the next calls, so a single blip does not move the instance to
    ///         local budgets; only after <see cref="FailuresBeforeFallback" /> failures in a row is Redis left
    ///         alone for the retry interval. Errors that will not go away on their own (wrong key type,
    ///         read-only replica, out of memory, script error, a command the client refuses to send) are
    ///         logged as errors instead of warnings, so a misconfiguration does not hide behind the fallback.
    ///     </para>
    /// </summary>
    internal sealed class RedisRateLimiter : RateLimiter
    {
        internal static readonly TimeSpan DefaultRedisRetryInterval = TimeSpan.FromSeconds(5);
        internal const int FailuresBeforeFallback = 3;
        internal static readonly TimeSpan MaxPollDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _releasableRetryAfterCap = TimeSpan.FromSeconds(1);
        private const double MaxPollJitterInMs = 250;

        /// <summary>Server errors that say "try again later" instead of "this cannot work".</summary>
        private static readonly string[] _transientServerErrors =
            ["LOADING", "BUSY", "MASTERDOWN", "TRYAGAIN", "CLUSTERDOWN"];

        private readonly Func<IDatabase> _database;
        private readonly RedisRateLimiterDefinition _definition;
        private readonly RedisKey[] _keys;
        private readonly Lazy<RateLimiter> _fallback;
        private readonly ILogger _logger;
        private readonly long _redisRetryIntervalInMs;
        private readonly ConcurrentDictionary<string, int> _heldLeases = new();
        private readonly ConcurrentDictionary<string, PendingRelease> _pendingReleases = new();
        private readonly object _renewalLock = new();
        private Timer _renewal;
        private bool _disposed;
        private int _renewing;
        private long _skipRedisUntil;
        private long _lastFailureWarning = long.MinValue;
        private int _consecutiveFailures;
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
            ThrowIfDisposed();
            EnsurePermitCount(permitCount);
            if (IsRedisSkipped()) return _fallback.Value.AttemptAcquire(permitCount);
            var leaseId = NewLeaseId();
            Attempt? attempt;
            try
            {
                var result = _database().ScriptEvaluate(_definition.Script, _keys,
                    _definition.Arguments(permitCount, leaseId));
                attempt = Succeeded(Parse(result, leaseId));
            }
            catch (Exception e) when (IsRedisFailure(e))
            {
                RecordFailure(e, leaseId, permitCount);
                attempt = null;
            }

            return attempt is null
                ? _fallback.Value.AttemptAcquire(permitCount)
                : ToLease(attempt.Value, permitCount);
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
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
                    ThrowIfDisposed();
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

        /// <summary>Returns null when Redis did not answer and the in-process limiter has to decide.</summary>
        private async Task<Attempt?> TryAcquireAsync(int permitCount)
        {
            if (IsRedisSkipped()) return null;
            var leaseId = NewLeaseId();
            try
            {
                var result = await _database().ScriptEvaluateAsync(_definition.Script, _keys,
                    _definition.Arguments(permitCount, leaseId));
                return Succeeded(Parse(result, leaseId));
            }
            catch (Exception e) when (IsRedisFailure(e))
            {
                RecordFailure(e, leaseId, permitCount);
                return null;
            }
        }

        private Attempt Succeeded(Attempt attempt)
        {
            Volatile.Write(ref _consecutiveFailures, 0);
            return attempt;
        }

        /// <summary>
        ///     The call that failed is decided by the in-process limiter. The script may have run
        ///     although no answer arrived, so the permits of a releasable limiter are handed back. Once Redis has
        ///     failed often enough in a row it is left alone for the retry interval.
        /// </summary>
        private void RecordFailure(Exception e, string leaseId, int permitCount)
        {
            if (leaseId is not null && permitCount > 0) _ = ReleaseAsync(leaseId, permitCount);
            if (Interlocked.Increment(ref _consecutiveFailures) >= FailuresBeforeFallback)
            {
                SkipRedis(e);
                return;
            }

            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastFailureWarning);
            if ((last == long.MinValue || now - last >= _redisRetryIntervalInMs) &&
                Interlocked.CompareExchange(ref _lastFailureWarning, now, last) == last)
                _logger.Log(GetLogLevel(e), e,
                    "Redis rate limiter {RateLimitKey} failed, the call is limited in-process",
                    _definition.Key.ToString());
        }

        private void EnsurePermitCount(int permitCount)
        {
            if (permitCount > _definition.PermitLimit)
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount,
                    $"{permitCount} permits exceeds the permit limit of {_definition.PermitLimit}.");
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

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

            if (!_definition.Releasable || permitCount == 0) return RedisRateLimitLease.Acquired();
            if (Volatile.Read(ref _disposed))
            {
                // disposed while Redis answered: nobody would renew the lease, so it is not handed out
                _ = ReleaseAsync(attempt.LeaseId, permitCount);
                ThrowIfDisposed();
            }

            Hold(attempt.LeaseId, permitCount);
            return RedisRateLimitLease.Acquired(disposed => Release(attempt.LeaseId, permitCount, disposed));
        }

        /// <summary>Remembers a lease so that it is renewed for as long as its holder keeps it.</summary>
        private void Hold(string leaseId, int permitCount)
        {
            if (_definition.LeaseTtl is null) return;
            _heldLeases[leaseId] = permitCount;
            EnsureTimer();
        }

        private void EnsureTimer()
        {
            if (_definition.LeaseTtl is not { } leaseTtl) return;
            lock (_renewalLock)
            {
                if (_disposed || _renewal is not null) return;
                var interval = TimeSpan.FromMilliseconds(Math.Max(1, leaseTtl.TotalMilliseconds / 3));
                _renewal = new Timer(_ => _ = RenewHeldLeasesAsync(), null, interval, interval);
            }
        }

        /// <summary>
        ///     Extends every lease this instance still holds and retries releases that did not reach Redis.
        ///     Runs on a timer that only exists while there is something to do, so an idle limiter keeps
        ///     nothing alive. A tick that finds the previous one still running is skipped.
        /// </summary>
        internal async Task RenewHeldLeasesAsync()
        {
            if (Interlocked.Exchange(ref _renewing, 1) == 1) return;
            try
            {
                lock (_renewalLock)
                {
                    if (_heldLeases.IsEmpty && _pendingReleases.IsEmpty)
                    {
                        _renewal?.Dispose();
                        _renewal = null;
                        return;
                    }
                }

                await RetryPendingReleasesAsync();
                await RenewAsync();
            }
            finally
            {
                Volatile.Write(ref _renewing, 0);
            }
        }

        private async Task RenewAsync()
        {
            if (_heldLeases.IsEmpty) return;
            try
            {
                var members = _heldLeases.ToArray().SelectMany(x => Members(x.Key, x.Value));
                RedisValue[] arguments = [(long)_definition.LeaseTtl!.Value.TotalMilliseconds, .. members];
                var lost = await _database().ScriptEvaluateAsync(RedisRateLimitScripts.RenewConcurrency, _keys,
                    arguments);
                ForgetLostLeases(lost);
            }
            catch (Exception e)
            {
                // runs on a timer thread: nothing may escape, and a missed renewal is retried on the next tick
                _logger.LogDebug(e, "Could not renew rate limit leases on {RateLimitKey}",
                    _definition.Key.ToString());
            }
        }

        /// <summary>
        ///     A held lease that is gone from Redis expired before it was renewed: Redis was unreachable or the
        ///     process paused for longer than the lease TTL. Its permits may already belong to somebody else,
        ///     and its holder keeps running, so the limit can be exceeded until the holder finishes.
        /// </summary>
        private void ForgetLostLeases(RedisResult lost)
        {
            if (lost is null || lost.IsNull || lost.Resp2Type != ResultType.Array) return;
            var leaseIds = ((RedisResult[])lost!).Select(x => (string)x)
                .Where(x => x?.Contains(':') == true)
                .Select(x => x[..x.LastIndexOf(':')])
                .Distinct();
            foreach (var leaseId in leaseIds)
                if (_heldLeases.TryRemove(leaseId, out _))
                    _logger.LogWarning(
                        "Rate limit lease {LeaseId} on {RateLimitKey} expired before it could be renewed. Its " +
                        "permits may have been handed out again, so the concurrency limit can be exceeded until " +
                        "the call that holds it finishes", leaseId, _definition.Key.ToString());
        }

        /// <param name="leaseId">The lease to give back.</param>
        /// <param name="permitCount">Number of permits the lease holds.</param>
        /// <param name="disposed">
        ///     False when the lease was garbage collected without being disposed. It then only stops being
        ///     renewed and expires on its own - a finalizer must not talk to Redis.
        /// </param>
        private void Release(string leaseId, int permitCount, bool disposed)
        {
            _heldLeases.TryRemove(leaseId, out _);
            if (disposed) _ = ReleaseAsync(leaseId, permitCount);
        }

        /// <summary>
        ///     Removes the members of a lease. A release that fails would keep the permits taken for the whole
        ///     lease TTL, for every process, so it is retried with the renewals until the TTL has passed.
        /// </summary>
        private async Task ReleaseAsync(string leaseId, int permitCount)
        {
            try
            {
                await _database().SortedSetRemoveAsync(_definition.Key, Members(leaseId, permitCount).ToArray());
                _pendingReleases.TryRemove(leaseId, out _);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Could not release rate limit lease on {RateLimitKey}, it is retried",
                    _definition.Key.ToString());
                if (_definition.LeaseTtl is not { } leaseTtl || Volatile.Read(ref _disposed)) return;
                _pendingReleases.TryAdd(leaseId,
                    new PendingRelease(permitCount, Environment.TickCount64 + (long)leaseTtl.TotalMilliseconds));
                EnsureTimer();
            }
        }

        private async Task RetryPendingReleasesAsync()
        {
            foreach (var (leaseId, pending) in _pendingReleases.ToArray())
            {
                // past the lease TTL the members have expired in Redis anyway
                if (Environment.TickCount64 >= pending.GiveUpAt)
                    _pendingReleases.TryRemove(leaseId, out _);
                else
                    await ReleaseAsync(leaseId, pending.PermitCount);
            }
        }

        private static IEnumerable<RedisValue> Members(string leaseId, int permitCount) =>
            Enumerable.Range(1, permitCount).Select(i => (RedisValue)$"{leaseId}:{i}");

        /// <summary>
        ///     Time based limiters free permits exactly when the script says so. Releasable permits can come
        ///     back at any moment, so those are polled with a short, growing interval instead. The delay is
        ///     capped, so a long window is polled now and then instead of slept through, and the jitter is
        ///     bounded, so a waiter does not wake up long after the permits came back.
        /// </summary>
        internal TimeSpan GetPollDelay(TimeSpan retryAfter, int round)
        {
            var delay = Math.Min(retryAfter.TotalMilliseconds, MaxPollDelay.TotalMilliseconds);
            if (_definition.Releasable)
                delay = Math.Min(delay, Math.Min(25d * Math.Pow(2, Math.Min(round, 8)), 500d));
            var jitter = Math.Min(delay * 0.2, MaxPollJitterInMs) * Random.Shared.NextDouble();
            return TimeSpan.FromMilliseconds(1 + delay + jitter);
        }

        /// <summary>
        ///     Every error that comes from Redis or the client: a rate limiter must not fail the calls it guards
        ///     because Redis is down or misconfigured. Anything else is a bug and is thrown.
        /// </summary>
        internal static bool IsRedisFailure(Exception e) =>
            e is RedisException or RedisCommandException or TimeoutException or ObjectDisposedException;

        /// <summary>
        ///     True for errors that never go away on their own: an answer from the server other than "try again
        ///     later", or a command the client refuses to send (command map, cross-slot).
        /// </summary>
        internal static bool IsConfigurationError(Exception e) => e switch
        {
            RedisServerException server => !_transientServerErrors.Any(x =>
                server.Message.StartsWith(x, StringComparison.Ordinal)),
            RedisCommandException => true,
            _ => false
        };

        private static LogLevel GetLogLevel(Exception e) =>
            IsConfigurationError(e) ? LogLevel.Error : LogLevel.Warning;

        private bool IsRedisSkipped() => Environment.TickCount64 < Volatile.Read(ref _skipRedisUntil);

        private void SkipRedis(Exception e)
        {
            Volatile.Write(ref _skipRedisUntil, Environment.TickCount64 + _redisRetryIntervalInMs);
            _logger.Log(GetLogLevel(e), e,
                "Redis rate limiter {RateLimitKey} is unavailable, limiting in-process for the next {RetryInMs} ms",
                _definition.Key.ToString(), _redisRetryIntervalInMs);
        }

        /// <summary>
        ///     Stops renewing. Leases that are still held are not taken away from their holders, they simply
        ///     expire after the lease TTL unless they are disposed before. Acquiring afterwards throws.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_renewalLock)
                {
                    Volatile.Write(ref _disposed, true);
                    _renewal?.Dispose();
                    _renewal = null;
                }

                if (_fallback.IsValueCreated) _fallback.Value.Dispose();
            }

            base.Dispose(disposing);
        }

        private readonly record struct Attempt(bool Acquired, TimeSpan RetryAfter, string LeaseId);

        private readonly record struct PendingRelease(int PermitCount, long GiveUpAt);
    }
}
