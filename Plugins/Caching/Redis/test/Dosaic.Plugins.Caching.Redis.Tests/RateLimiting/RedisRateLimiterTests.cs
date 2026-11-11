using System.Runtime.CompilerServices;
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
            TimeSpan? redisRetryInterval = null, TimeSpan? leaseTtl = null) =>
            new(() => _database, new RedisRateLimiterDefinition
            {
                Script = Script,
                Key = Key,
                PermitLimit = permitLimit,
                QueueLimit = queueLimit,
                Releasable = releasable,
                LeaseTtl = leaseTtl,
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

        private void RedisThrows(Exception exception)
        {
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).ThrowsAsync(exception);
            _database.ScriptEvaluate(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).Throws(exception);
        }

        [Test]
        public async Task RedisErrorsLetTheCallThroughTheInProcessLimiterAndRepeatedOnesSkipRedis()
        {
            RedisFails();
            var limiter = CreateLimiter();

            var acquired = new List<bool>();
            for (var i = 0; i < RedisRateLimiter.FailuresBeforeFallback + 2; i++)
            {
                using var lease = await limiter.AcquireAsync();
                acquired.Add(lease.IsAcquired);
            }

            acquired.Should().OnlyContain(x => x, "an outage never rejects a call, the in-process limiter decides");
            AsyncScriptCalls().Should().Be(RedisRateLimiter.FailuresBeforeFallback,
                "Redis is left alone until the retry interval has passed");
            _logger.Entries.Where(x => x.Level == LogLevel.Warning).Should().HaveCount(2)
                .And.OnlyContain(x => x.Exception is RedisConnectionException);
        }

        [Test]
        public async Task TheInProcessLimiterStillLimitsDuringAnOutage()
        {
            RedisFails();
            var limiter = CreateLimiter();

            using var first = await limiter.AcquireAsync();
            using var second = await limiter.AcquireAsync();

            first.IsAcquired.Should().BeTrue("the in-process limiter has one permit");
            second.IsAcquired.Should().BeFalse("the in-process limiter is exhausted");
        }

        [Test]
        public async Task ASuccessfulCallResetsTheFailureCount()
        {
            var failure = new RedisTimeoutException("too slow", CommandStatus.Unknown);
            _database.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                    Arg.Any<CommandFlags>())
                .Returns(_ => Task.FromException<RedisResult>(failure), _ => Task.FromException<RedisResult>(failure),
                    _ => Task.FromResult(Granted()), _ => Task.FromException<RedisResult>(failure),
                    _ => Task.FromException<RedisResult>(failure), _ => Task.FromResult(Granted()));
            var limiter = CreateLimiter();

            var acquired = new List<bool>();
            for (var i = 0; i < 6; i++)
            {
                using var lease = await limiter.AcquireAsync();
                acquired.Add(lease.IsAcquired);
            }

            acquired.Should().OnlyContain(x => x);
            AsyncScriptCalls().Should().Be(6, "two failures in a row never stop asking Redis");
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
        public async Task ATimeoutOfAReleasableLimiterHandsBackTheLeaseTheScriptMayHaveTaken()
        {
            RedisValue[] arguments = null;
            _database.ScriptEvaluateAsync(Script, Arg.Any<RedisKey[]>(), Arg.Do<RedisValue[]>(x => arguments = x),
                    Arg.Any<CommandFlags>())
                .ThrowsAsync(new RedisTimeoutException("too slow", CommandStatus.Sent));
            var limiter = CreateLimiter(releasable: true);

            using var lease = await limiter.AcquireAsync();

            lease.IsAcquired.Should().BeTrue("the in-process limiter lets the call through");
            var leaseId = arguments[1].ToString();
            await _database.Received(1).SortedSetRemoveAsync(Key,
                Arg.Is<RedisValue[]>(m => m.Length == 1 && m[0] == $"{leaseId}:1"), Arg.Any<CommandFlags>());
        }

        [TestCaseSource(nameof(TransientErrors))]
        public void ErrorsThatGoAwayOnTheirOwnAreLoggedAsWarnings(Exception exception)
        {
            RedisRateLimiter.IsRedisFailure(exception).Should().BeTrue();
            RedisRateLimiter.IsConfigurationError(exception).Should().BeFalse();
        }

        private static IEnumerable<Exception> TransientErrors() =>
        [
            new RedisConnectionException(ConnectionFailureType.UnableToConnect, "gone"),
            new RedisTimeoutException("too slow", CommandStatus.Sent),
            new TimeoutException("too slow"),
            new ObjectDisposedException("multiplexer"),
            new RedisServerException("LOADING Redis is loading the dataset in memory"),
            new RedisServerException("BUSY Redis is busy running a script"),
            new RedisServerException("MASTERDOWN Link with MASTER is down"),
            new RedisServerException("TRYAGAIN Multiple keys request during rehashing of slot"),
            new RedisServerException("CLUSTERDOWN The cluster is down")
        ];

        [TestCaseSource(nameof(ConfigurationErrors))]
        public async Task ConfigurationErrorsLetTheCallThroughAndAreLoggedAsErrors(Exception exception)
        {
            RedisThrows(exception);
            var limiter = CreateLimiter();

            using (var lease = await limiter.AcquireAsync())
                lease.IsAcquired.Should().BeTrue("a misconfigured Redis must not fail the guarded call");
            using (var lease = limiter.AttemptAcquire())
                lease.IsAcquired.Should().BeTrue();

            RedisRateLimiter.IsConfigurationError(exception).Should().BeTrue();
            _logger.Entries.Should().Contain(x => x.Level == LogLevel.Error && x.Exception == exception);
            _logger.Entries.Should().NotContain(x => x.Level == LogLevel.Warning);
        }

        private static IEnumerable<Exception> ConfigurationErrors() =>
        [
            new RedisServerException("WRONGTYPE Operation against a key holding the wrong kind of value"),
            new RedisServerException("READONLY You can't write against a read only replica."),
            new RedisServerException("OOM command not allowed when used memory > 'maxmemory'."),
            new RedisServerException("ERR Error running script"),
            new RedisCommandException("This operation has been disabled in the command-map")
        ];

        [Test]
        public async Task BugsAreNotSwallowed()
        {
            RedisThrows(new InvalidOperationException("a bug"));
            var limiter = CreateLimiter();

            await limiter.Invoking(x => x.AcquireAsync().AsTask()).Should().ThrowAsync<InvalidOperationException>();
            limiter.Invoking(x => x.AttemptAcquire()).Should().Throw<InvalidOperationException>();
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
            _logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning);
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
        public void TheSynchronousPathLetsCallsThroughOnRedisErrors()
        {
            RedisFails();
            var limiter = CreateLimiter();

            var acquired = new List<bool>();
            for (var i = 0; i < RedisRateLimiter.FailuresBeforeFallback + 1; i++)
            {
                using var lease = limiter.AttemptAcquire();
                acquired.Add(lease.IsAcquired);
            }

            acquired.Should().OnlyContain(x => x);
            SyncScriptCalls().Should().Be(RedisRateLimiter.FailuresBeforeFallback);
            _logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning && x.Message.Contains("unavailable"));
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
            await _database.Received(1).SortedSetRemoveAsync(Key,
                Arg.Is<RedisValue[]>(m => m.Length == 2 && m[0] == $"{leaseId}:1" && m[1] == $"{leaseId}:2"),
                CommandFlags.None);
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

        private int ReleaseCalls() => _database.ReceivedCalls()
            .Count(x => x.GetMethodInfo().Name == nameof(IDatabase.SortedSetRemoveAsync));

        [Test]
        public async Task AFailingReleaseIsRetriedWithTheRenewalsUntilItSucceeds()
        {
            RedisAnswers(Granted());
            _database.SortedSetRemoveAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
                .Returns(_ => Task.FromException<long>(new RedisConnectionException(ConnectionFailureType.SocketClosed,
                    "gone")), _ => Task.FromResult(1L));
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            var lease = await limiter.AcquireAsync();

            lease.Invoking(x => x.Dispose()).Should().NotThrow();
            ReleaseCalls().Should().Be(1);
            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Debug);

            await limiter.RenewHeldLeasesAsync();
            ReleaseCalls().Should().Be(2, "the release is retried with the next renewal");
            await limiter.RenewHeldLeasesAsync();
            ReleaseCalls().Should().Be(2, "the retried release went through");
        }

        [Test]
        public async Task AReleaseWithoutALeaseTtlIsNotRetried()
        {
            RedisAnswers(Granted());
            _database.SortedSetRemoveAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
                .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketClosed, "gone"));
            using var limiter = CreateLimiter(releasable: true);

            (await limiter.AcquireAsync()).Dispose();
            await limiter.RenewHeldLeasesAsync();

            ReleaseCalls().Should().Be(1);
        }

        private List<RedisValue[]> RenewalCalls() => _database.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(IDatabase.ScriptEvaluateAsync) &&
                        (string)x.GetArguments()[0] == RedisRateLimitScripts.RenewConcurrency)
            .Select(x => (RedisValue[])x.GetArguments()[2]).ToList();

        private static async Task WaitUntilAsync(Func<bool> condition, string because)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(10);
            }

            Assert.Fail($"Timed out waiting until {because}.");
        }

        [Test]
        public async Task HeldReleasableLeasesAreRenewedWithTheLeaseTtl()
        {
            RedisAnswers(Granted());
            var leaseIds = new List<string>();
            await _database.ScriptEvaluateAsync(Script, Arg.Any<RedisKey[]>(),
                Arg.Do<RedisValue[]>(x => leaseIds.Add(x[1])), Arg.Any<CommandFlags>());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            using var two = await limiter.AcquireAsync(2);
            using var one = await limiter.AcquireAsync();

            await limiter.RenewHeldLeasesAsync();

            var renewal = RenewalCalls().Should().ContainSingle().Which;
            renewal[0].Should().Be(120_000);
            renewal.Skip(1).Select(x => x.ToString()).Should()
                .BeEquivalentTo($"{leaseIds[0]}:1", $"{leaseIds[0]}:2", $"{leaseIds[1]}:1");
        }

        [Test]
        public async Task ReleasedLeasesAreNotRenewed()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            (await limiter.AcquireAsync()).Dispose();

            await limiter.RenewHeldLeasesAsync();

            RenewalCalls().Should().BeEmpty();
        }

        [Test]
        public async Task HeldLeasesAreRenewedOnATimerSeveralTimesPerLeaseTtl()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMilliseconds(90));
            using var lease = await limiter.AcquireAsync();

            await WaitUntilAsync(() => RenewalCalls().Count >= 3, "the held lease was renewed three times");
        }

        [Test]
        public async Task TheRenewalTimerStopsWhenNothingIsHeldAndStartsAgainWithTheNextLease()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMilliseconds(60));
            (await limiter.AcquireAsync()).Dispose();
            await Task.Delay(200);
            RenewalCalls().Should().BeEmpty();

            using var lease = await limiter.AcquireAsync();

            await WaitUntilAsync(() => RenewalCalls().Count >= 1, "the new lease is renewed by a new timer");
        }

        [Test]
        public async Task ADisposedLimiterStopsRenewing()
        {
            RedisAnswers(Granted());
            var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMilliseconds(60));
            using var lease = await limiter.AcquireAsync();

            limiter.Dispose();
            _database.ClearReceivedCalls();
            await Task.Delay(250);

            RenewalCalls().Should().BeEmpty("a stopped instance must let its leases expire");
        }

        [Test]
        public async Task LeasesWithoutALeaseTtlAreNeverRenewed()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true);
            using var lease = await limiter.AcquireAsync();

            await limiter.RenewHeldLeasesAsync();

            RenewalCalls().Should().BeEmpty();
        }

        [Test]
        public async Task AFailingRenewalIsLoggedAndRetriedOnTheNextTick()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            using var lease = await limiter.AcquireAsync();
            _database.ScriptEvaluateAsync(RedisRateLimitScripts.RenewConcurrency, Arg.Any<RedisKey[]>(),
                    Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
                .ThrowsAsync(new InvalidOperationException("boom"));

            await limiter.Invoking(x => x.RenewHeldLeasesAsync()).Should().NotThrowAsync();
            await limiter.RenewHeldLeasesAsync();

            RenewalCalls().Should().HaveCount(2);
            _logger.Entries.Should().HaveCount(2).And.OnlyContain(x => x.Level == LogLevel.Debug);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task AcquireAndDropAsync(RedisRateLimiter limiter) =>
            (await limiter.AcquireAsync()).IsAcquired.Should().BeTrue();

        [Test]
        public async Task ALeaseThatWasDroppedWithoutDisposeStopsBeingRenewedButIsNotReleased()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            await AcquireAndDropAsync(limiter);

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            await limiter.RenewHeldLeasesAsync();

            RenewalCalls().Should().BeEmpty("nobody holds the lease any more, so it has to expire");
            ReleaseCalls().Should().Be(0);
        }

        [Test]
        public async Task TimeBasedLeasesHaveNothingToRelease()
        {
            RedisAnswers(Granted());
            var limiter = CreateLimiter();

            (await limiter.AcquireAsync()).Dispose();

            ReleaseCalls().Should().Be(0);
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
            var limiter = CreateLimiter(redisRetryInterval: TimeSpan.FromMinutes(1));
            for (var i = 0; i < RedisRateLimiter.FailuresBeforeFallback; i++)
                (await limiter.AcquireAsync()).Dispose();

            limiter.GetStatistics().Should().BeNull();
            limiter.IdleDuration.Should().BeNull();
            limiter.Invoking(x => x.Dispose()).Should().NotThrow();
            CreateLimiter().Invoking(x => x.Dispose()).Should().NotThrow();
        }

        [Test]
        public async Task LostLeasesAreLoggedAndNoLongerRenewed()
        {
            var leaseIds = new List<string>();
            _database.ScriptEvaluateAsync(Script, Arg.Any<RedisKey[]>(), Arg.Do<RedisValue[]>(x => leaseIds.Add(x[1])),
                Arg.Any<CommandFlags>()).Returns(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            using var lost = await limiter.AcquireAsync(2);
            using var kept = await limiter.AcquireAsync();
            _database.ScriptEvaluateAsync(RedisRateLimitScripts.RenewConcurrency, Arg.Any<RedisKey[]>(),
                    Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
                .Returns(RedisResult.Create(new RedisValue[] { $"{leaseIds[0]}:1", $"{leaseIds[0]}:2" }));

            await limiter.RenewHeldLeasesAsync();
            await limiter.RenewHeldLeasesAsync();

            _logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning)
                .Which.Message.Should().Contain(leaseIds[0]);
            RenewalCalls()[^1].Skip(1).Select(x => x.ToString()).Should().Equal($"{leaseIds[1]}:1");
        }

        [Test]
        public async Task ARenewalTickIsSkippedWhileThePreviousOneIsStillRunning()
        {
            RedisAnswers(Granted());
            using var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));
            using var lease = await limiter.AcquireAsync();
            var slowRenewal = new TaskCompletionSource<RedisResult>();
            _database.ScriptEvaluateAsync(RedisRateLimitScripts.RenewConcurrency, Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>()).Returns(slowRenewal.Task);

            var first = limiter.RenewHeldLeasesAsync();
            await limiter.RenewHeldLeasesAsync();
            RenewalCalls().Should().HaveCount(1);
            slowRenewal.SetResult(RedisResult.Create(Array.Empty<RedisValue>()));
            await first;
            await limiter.RenewHeldLeasesAsync();

            RenewalCalls().Should().HaveCount(2);
        }

        [Test]
        public async Task ADisposedLimiterRefusesToAcquire()
        {
            RedisAnswers(Granted());
            var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));

            limiter.Dispose();

            limiter.Invoking(x => x.AttemptAcquire()).Should().Throw<ObjectDisposedException>();
            await limiter.Invoking(x => x.AcquireAsync().AsTask()).Should().ThrowAsync<ObjectDisposedException>();
            AsyncScriptCalls().Should().Be(0);
            SyncScriptCalls().Should().Be(0);
        }

        [Test]
        public async Task ALimiterDisposedWhileRedisAnswersHandsTheLeaseBack()
        {
            var answer = new TaskCompletionSource<RedisResult>();
            _database.ScriptEvaluateAsync(Script, Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>()).Returns(answer.Task);
            var limiter = CreateLimiter(releasable: true, leaseTtl: TimeSpan.FromMinutes(2));

            var acquire = limiter.AcquireAsync().AsTask();
            limiter.Dispose();
            answer.SetResult(Granted());

            await acquire.Invoking(x => x).Should().ThrowAsync<ObjectDisposedException>();
            ReleaseCalls().Should().Be(1);
        }

        [Test]
        public void ThePollDelayIsCappedAndItsJitterBounded()
        {
            using var timeBased = CreateLimiter();
            using var releasable = CreateLimiter(releasable: true);

            for (var i = 0; i < 50; i++)
            {
                timeBased.GetPollDelay(TimeSpan.FromHours(1), 0).Should()
                    .BeLessThanOrEqualTo(RedisRateLimiter.MaxPollDelay + TimeSpan.FromMilliseconds(251));
                timeBased.GetPollDelay(TimeSpan.FromDays(60), 0).Should()
                    .BeLessThanOrEqualTo(RedisRateLimiter.MaxPollDelay + TimeSpan.FromMilliseconds(251));
                timeBased.GetPollDelay(TimeSpan.FromMilliseconds(100), 0).Should()
                    .BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(121));
                releasable.GetPollDelay(TimeSpan.FromMinutes(2), 20).Should()
                    .BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(601));
            }
        }
    }
}
