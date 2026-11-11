using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Plugins.Caching.Redis.RateLimiting;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using StackExchange.Redis;

namespace Dosaic.Plugins.Caching.Redis.Tests.RateLimiting
{
    public class RedisRateLimiterTests
    {
        private const string Script = "return {1, 0}";
        private const string Key = "ratelimit:client:test";
        private IDatabase _database;
        private FakeLogger<RedisRateLimiter> _logger;

        [SetUp]
        public void Up()
        {
            _database = Substitute.For<IDatabase>();
            _logger = new FakeLogger<RedisRateLimiter>();
        }

        private RedisRateLimiter CreateLimiter(int permitLimit = 5, int queueLimit = 0, bool releasable = false,
            TimeSpan? redisRetryInterval = null) =>
            new(() => _database, new RedisRateLimiterDefinition
            {
                Script = Script,
                Key = Key,
                PermitLimit = permitLimit,
                QueueLimit = queueLimit,
                Releasable = releasable,
                Arguments = (permits, leaseId) => releasable ? [permits, leaseId] : [permits],
                CreateFallback = () => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 0
                })
            }, _logger, redisRetryInterval);

        private static RedisResult Granted() => RedisResult.Create(new RedisValue[] { 1, 0 });

        private static RedisResult Rejected(int retryAfterInMs) =>
            RedisResult.Create(new RedisValue[] { 0, retryAfterInMs });

        private void RedisAnswers(RedisResult first, params RedisResult[] next)
        {
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).Returns(first, next);
            _database.ScriptEvaluate(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).Returns(first, next);
        }

        private void RedisFails()
        {
            var failure = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis is gone");
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).ThrowsAsync(failure);
            _database.ScriptEvaluate(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).Throws(failure);
        }

        private int AsyncScriptCalls() => _database.ReceivedCalls()
            .Count(x => x.GetMethodInfo().Name == nameof(IDatabase.ScriptEvaluateAsync));

        private int SyncScriptCalls() => _database.ReceivedCalls()
            .Count(x => x.GetMethodInfo().Name == nameof(IDatabase.ScriptEvaluate));

        private static TimeSpan? RetryAfter(RateLimitLease lease) =>
            lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : null;

        [Test]
        public async Task AcquiresWhenRedisGrantsThePermits()
        {
            RedisAnswers(Granted());
            var limiter = CreateLimiter();

            using var lease = await limiter.AcquireAsync(2);

            lease.IsAcquired.Should().BeTrue();
            lease.MetadataNames.Should().BeEmpty();
            RetryAfter(lease).Should().BeNull();
            await _database.Received(1).ScriptEvaluateAsync(Script,
                Arg.Is<RedisKey[]>(k => k.Length == 1 && k[0] == Key),
                Arg.Is<RedisValue[]>(v => v.Length == 1 && v[0] == 2), Arg.Any<CommandFlags>());
        }

        [Test]
        public async Task RejectsWithRetryAfterWhenNothingMayWait()
        {
            RedisAnswers(Rejected(1500));
            var limiter = CreateLimiter();

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeFalse();
            lease.MetadataNames.Should().Equal(MetadataName.RetryAfter.Name);
            RetryAfter(lease).Should().Be(TimeSpan.FromMilliseconds(1500));
            lease.TryGetMetadata("unknown", out var unknown).Should().BeFalse();
            unknown.Should().BeNull();
            AsyncScriptCalls().Should().Be(1);
        }

        [Test]
        public async Task WaitsAndPollsUntilAPermitIsFree()
        {
            RedisAnswers(Rejected(10), Rejected(10), Granted());
            var limiter = CreateLimiter(queueLimit: 1);

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeTrue();
            AsyncScriptCalls().Should().Be(3);
        }

        [Test]
        public async Task ReleasableLimitersPollFasterThanTheLeaseExpiry()
        {
            RedisAnswers(Rejected(60_000), Rejected(60_000), Granted());
            var limiter = CreateLimiter(queueLimit: 1, releasable: true);

            using var lease = await limiter.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

            lease.IsAcquired.Should().BeTrue();
        }

        [Test]
        public async Task TheQueueLimitBoundsTheWaitersOfThisInstance()
        {
            RedisAnswers(Rejected(60_000));
            var limiter = CreateLimiter(queueLimit: 1);
            using var cancellation = new CancellationTokenSource();

            var waiting = limiter.AcquireAsync(1, cancellation.Token).AsTask();
            using var rejected = await limiter.AcquireAsync();

            rejected.IsAcquired.Should().BeFalse();
            RetryAfter(rejected).Should().Be(TimeSpan.FromSeconds(60));
            waiting.IsCompleted.Should().BeFalse();
            await cancellation.CancelAsync();
            await waiting.Invoking(x => x).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task AWaiterThatGaveUpFreesItsQueueSlot()
        {
            RedisAnswers(Rejected(60_000));
            var limiter = CreateLimiter(queueLimit: 1);
            using var cancellation = new CancellationTokenSource();
            var first = limiter.AcquireAsync(1, cancellation.Token).AsTask();
            await cancellation.CancelAsync();
            await first.Invoking(x => x).Should().ThrowAsync<OperationCanceledException>();
            using var secondCancellation = new CancellationTokenSource();

            var second = limiter.AcquireAsync(1, secondCancellation.Token).AsTask();
            await Task.Delay(50);

            second.IsCompleted.Should().BeFalse("the slot of the cancelled waiter is free again");
            await secondCancellation.CancelAsync();
            await second.Invoking(x => x).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task ACancelledTokenNeverReachesRedis()
        {
            var limiter = CreateLimiter();

            await limiter.Invoking(x => x.AcquireAsync(1, new CancellationToken(true)).AsTask()).Should()
                .ThrowAsync<OperationCanceledException>();

            AsyncScriptCalls().Should().Be(0);
        }

        [Test]
        public async Task ProbingForZeroPermitsNeverWaits()
        {
            RedisAnswers(Rejected(60_000));
            var limiter = CreateLimiter(queueLimit: 10);

            using var lease = await limiter.AcquireAsync(0);

            lease.IsAcquired.Should().BeFalse();
        }

        [Test]
        public async Task MorePermitsThanTheLimitAreRefused()
        {
            var limiter = CreateLimiter(permitLimit: 2);

            limiter.Invoking(x => x.AttemptAcquire(3)).Should().Throw<ArgumentOutOfRangeException>();
            await limiter.Invoking(x => x.AcquireAsync(3).AsTask()).Should()
                .ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task RedisErrorsFallBackToTheInProcessLimiterAndLogAWarning()
        {
            RedisFails();
            var limiter = CreateLimiter();

            using var first = await limiter.AcquireAsync();
            using var second = await limiter.AcquireAsync();

            first.IsAcquired.Should().BeTrue("the in-process limiter has one permit");
            second.IsAcquired.Should().BeFalse("the in-process limiter is exhausted");
            AsyncScriptCalls().Should().Be(1, "Redis is left alone until the retry interval has passed");
            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning)
                .Which.Exception.Should().BeOfType<RedisConnectionException>();
        }

        [Test]
        public async Task RedisIsAskedAgainOnceTheRetryIntervalHasPassed()
        {
            RedisFails();
            var limiter = CreateLimiter(redisRetryInterval: TimeSpan.Zero);

            using var first = await limiter.AcquireAsync();
            using var second = await limiter.AcquireAsync();

            AsyncScriptCalls().Should().Be(2);
        }

        [Test]
        public async Task RedisTimeoutsFallBackAsWell()
        {
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).ThrowsAsync(new TimeoutException("too slow"));
            var limiter = CreateLimiter();

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeTrue();
        }

        [Test]
        public async Task OtherErrorsAreNotSwallowed()
        {
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).ThrowsAsync(new InvalidOperationException("a bug"));
            var limiter = CreateLimiter();

            await limiter.Invoking(x => x.AcquireAsync().AsTask()).Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task AWaiterMovesToTheInProcessLimiterWhenRedisFailsWhileWaiting()
        {
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                    Arg.Any<CommandFlags>())
                .Returns(_ => Task.FromResult(Rejected(10)),
                    _ => Task.FromException<RedisResult>(new RedisTimeoutException("too slow", CommandStatus.Unknown)));
            var limiter = CreateLimiter(queueLimit: 1);

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeTrue();
            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning);
        }

        [Test]
        public void TheSynchronousPathAcquiresAndRejects()
        {
            RedisAnswers(Granted(), Rejected(250));
            var limiter = CreateLimiter(queueLimit: 5);

            using var granted = limiter.AttemptAcquire();
            using var rejected = limiter.AttemptAcquire();

            granted.IsAcquired.Should().BeTrue();
            rejected.IsAcquired.Should().BeFalse();
            RetryAfter(rejected).Should().Be(TimeSpan.FromMilliseconds(250));
            SyncScriptCalls().Should().Be(2);
        }

        [Test]
        public void TheSynchronousPathFallsBackOnRedisErrors()
        {
            RedisFails();
            var limiter = CreateLimiter();

            using var first = limiter.AttemptAcquire();
            using var second = limiter.AttemptAcquire();

            first.IsAcquired.Should().BeTrue();
            second.IsAcquired.Should().BeFalse();
            SyncScriptCalls().Should().Be(1);
            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning);
        }

        [Test]
        public async Task DisposingAReleasableLeaseHandsThePermitsBackOnce()
        {
            RedisAnswers(Granted());
            RedisValue[] arguments = null;
            await _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(),
                Arg.Do<RedisValue[]>(x => arguments = x), Arg.Any<CommandFlags>());
            _database.ClearReceivedCalls();
            var limiter = CreateLimiter(releasable: true);

            var lease = await limiter.AcquireAsync(2);
            lease.Dispose();
            lease.Dispose();

            var leaseId = arguments[1].ToString();
            leaseId.Should().NotBeNullOrEmpty();
            _database.Received(1).SortedSetRemove(Key,
                Arg.Is<RedisValue[]>(m => m.Length == 2 && m[0] == $"{leaseId}:1" && m[1] == $"{leaseId}:2"),
                CommandFlags.FireAndForget);
        }

        [Test]
        public async Task EveryReleasableLeaseGetsItsOwnId()
        {
            RedisAnswers(Granted());
            var leaseIds = new List<string>();
            await _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(),
                Arg.Do<RedisValue[]>(x => leaseIds.Add(x[1])), Arg.Any<CommandFlags>());
            var limiter = CreateLimiter(releasable: true);

            using var first = await limiter.AcquireAsync();
            using var second = await limiter.AcquireAsync();

            leaseIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        }

        [Test]
        public async Task AFailingReleaseIsLeftToTheLeaseExpiry()
        {
            RedisAnswers(Granted());
            _database.SortedSetRemove(Arg.Any<RedisKey>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
                .Throws(new RedisConnectionException(ConnectionFailureType.SocketClosed, "gone"));
            var limiter = CreateLimiter(releasable: true);
            var lease = await limiter.AcquireAsync();

            lease.Invoking(x => x.Dispose()).Should().NotThrow();

            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Debug);
        }

        [Test]
        public async Task TimeBasedLeasesHaveNothingToRelease()
        {
            RedisAnswers(Granted());
            var limiter = CreateLimiter();

            (await limiter.AcquireAsync()).Dispose();

            _database.DidNotReceiveWithAnyArgs().SortedSetRemove(default, Array.Empty<RedisValue>());
        }

        [Test]
        public async Task ARejectedReleasableLeaseDoesNotAdvertiseTheLeaseExpiryAsRetryAfter()
        {
            RedisAnswers(Rejected(120_000), Rejected(300));
            var limiter = CreateLimiter(releasable: true);

            using var capped = await limiter.AcquireAsync();
            using var asIs = await limiter.AcquireAsync();

            RetryAfter(capped).Should().Be(TimeSpan.FromSeconds(1));
            RetryAfter(asIs).Should().Be(TimeSpan.FromMilliseconds(300));
        }

        [Test]
        public async Task HasNoLocalStatisticsAndDisposesItsFallback()
        {
            RedisFails();
            var limiter = CreateLimiter();
            using var lease = await limiter.AcquireAsync();

            limiter.GetStatistics().Should().BeNull();
            limiter.IdleDuration.Should().BeNull();
            limiter.Invoking(x => x.Dispose()).Should().NotThrow();
            CreateLimiter().Invoking(x => x.Dispose()).Should().NotThrow();
        }
    }
}
