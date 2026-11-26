using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Fetching
{
    public class JobQueuePrefetcherTests
    {
        private IJobQueueClient _client;
        private static readonly PrefetchSettings _settings = new()
        {
            PrefetchCount = 3,
            PollInterval = TimeSpan.FromMilliseconds(1),
            InvisibilityTimeout = TimeSpan.FromMinutes(5)
        };

        private static readonly DateTime _fetchedAt = new(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc);

        private static PrefetchedQueueEntry Entry(long queueEntryId, long jobId) =>
            new(queueEntryId, jobId, _fetchedAt);

        [SetUp]
        public void Up() => _client = Substitute.For<IJobQueueClient>();

        [Test]
        public void OneRoundTripServesTheWholePrefetchBuffer()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>())
                .Returns([Entry(10, 100), Entry(11, 101), Entry(12, 102)], []);
            var prefetcher = new JobQueuePrefetcher(_client, _settings);

            var jobIds = Enumerable.Range(0, 3)
                .Select(_ => prefetcher.Fetch(["bulk"], CancellationToken.None).JobId).ToList();

            jobIds.Should().Equal("100", "101", "102");
            _client.Received(1).Fetch(Arg.Is<string[]>(x => x[0] == "bulk"), 3, TimeSpan.FromMinutes(5));
        }

        [Test]
        public void EmptyQueuesAreRetriedUntilCancelled()
        {
            _client.Fetch(Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<TimeSpan>()).Returns([]);
            var prefetcher = new JobQueuePrefetcher(_client, _settings);
            using var cts = new CancellationTokenSource(50);

            prefetcher.Invoking(x => x.Fetch(["bulk"], cts.Token)).Should().Throw<OperationCanceledException>();
            _client.ReceivedWithAnyArgs().Fetch(default, default, default);
        }

        [Test]
        public void CancellationHandsUndispatchedEntriesBackToTheQueue()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>())
                .Returns([Entry(10, 100), Entry(11, 101), Entry(12, 102)]);
            var prefetcher = new JobQueuePrefetcher(_client, _settings);
            using var cts = new CancellationTokenSource();

            prefetcher.Fetch(["bulk"], cts.Token).JobId.Should().Be("100");
            cts.Cancel();
            prefetcher.Invoking(x => x.Fetch(["bulk"], cts.Token)).Should().Throw<OperationCanceledException>();

            // the two still buffered entries are released, the one handed to a worker is not
            _client.Received(1).Requeue(11, _fetchedAt);
            _client.Received(1).Requeue(12, _fetchedAt);
            _client.DidNotReceive().Requeue(10, Arg.Any<DateTime>());
        }

        [Test]
        public void DisposingReleasesTheBuffer()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>()).Returns([Entry(10, 100), Entry(11, 101)]);
            var prefetcher = new JobQueuePrefetcher(_client, _settings);
            prefetcher.Fetch(["bulk"], CancellationToken.None);

            prefetcher.Dispose();
            prefetcher.Dispose();

            _client.Received(1).Requeue(11, _fetchedAt);
        }

        [Test]
        public async Task BufferedEntriesAreKeptAliveInOneRoundTrip()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>()).Returns([Entry(10, 100), Entry(11, 101)], []);
            var renewed = _fetchedAt.AddMinutes(1);
            _client.KeepAlive(Arg.Any<IReadOnlyList<PrefetchedQueueEntry>>())
                .Returns(new Dictionary<long, DateTime> { [11] = renewed });
            using var prefetcher = new JobQueuePrefetcher(_client, new PrefetchSettings
            {
                PrefetchCount = 3,
                PollInterval = TimeSpan.FromMilliseconds(1),
                InvisibilityTimeout = TimeSpan.FromMinutes(5),
                SlidingKeepAliveInterval = TimeSpan.FromMilliseconds(10)
            });
            prefetcher.Fetch(["bulk"], CancellationToken.None).JobId.Should().Be("100");

            await Task.Delay(80);

            _client.Received().KeepAlive(Arg.Is<IReadOnlyList<PrefetchedQueueEntry>>(x =>
                x.Count == 1 && x[0].QueueEntryId == 11));
            // entry 11 was renewed, so the next fetch hands it out with the new timestamp
            prefetcher.Fetch(["bulk"], CancellationToken.None).JobId.Should().Be("101");
        }

        private static PrefetchSettings Polling(int prefetchCount, TimeSpan pollInterval) => new()
        {
            PrefetchCount = prefetchCount,
            PollInterval = pollInterval,
            InvisibilityTimeout = TimeSpan.FromMinutes(5)
        };

        // a dedicated thread per worker like Hangfire's, so blocked workers cannot starve the thread pool
        private static Task<Exception> FetchInBackground(JobQueuePrefetcher prefetcher, CancellationToken token) =>
            Task.Factory.StartNew(() =>
            {
                try
                {
                    prefetcher.Fetch(["bulk"], token);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        [Test]
        public async Task AnIdleQueueIsAskedOncePerIntervalNoMatterHowManyWorkersWait()
        {
            _client.Fetch(Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<TimeSpan>()).Returns([]);
            using var prefetcher = new JobQueuePrefetcher(_client, Polling(1, TimeSpan.FromMilliseconds(300)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var workers = Enumerable.Range(0, 20).Select(_ => FetchInBackground(prefetcher, cts.Token)).ToArray();
            var results = await Task.WhenAll(workers);

            results.Should().AllBeAssignableTo<OperationCanceledException>();
            // one round trip per started interval; 20 workers polling on their own would ask 20 times as often
            var intervals = (int)(stopwatch.ElapsedMilliseconds / 300) + 1;
            _client.ReceivedCalls().Count(x => x.GetMethodInfo().Name == nameof(IJobQueueClient.Fetch))
                .Should().BeInRange(1, intervals + 1);
        }

        [Test]
        public void ABacklogIsDrainedWithoutWaitingForTheInterval()
        {
            _client.Fetch(Arg.Any<string[]>(), 1, Arg.Any<TimeSpan>())
                .Returns([Entry(10, 100)], [Entry(11, 101)], [Entry(12, 102)], []);
            using var prefetcher = new JobQueuePrefetcher(_client, Polling(1, TimeSpan.FromSeconds(30)));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var jobIds = Enumerable.Range(0, 3)
                .Select(_ => prefetcher.Fetch(["bulk"], CancellationToken.None).JobId).ToList();

            jobIds.Should().Equal("100", "101", "102");
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task WaitingWorkersAreWokenWhenARoundTripFindsJobs()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>())
                .Returns([], [Entry(10, 100), Entry(11, 101), Entry(12, 102)], []);
            using var prefetcher = new JobQueuePrefetcher(_client, Polling(3, TimeSpan.FromMilliseconds(200)));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var workers = Enumerable.Range(0, 3).Select(_ => FetchInBackground(prefetcher, cts.Token)).ToArray();
            var results = await Task.WhenAll(workers);

            results.Should().AllSatisfy(x => x.Should().BeNull());
            _client.Received(2).Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>());
        }

        [Test]
        public async Task AFailingRoundTripIsNotRepeatedByEveryWaitingWorker()
        {
            _client.Fetch(Arg.Any<string[]>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                .Returns(_ => throw new InvalidOperationException("database down"));
            using var prefetcher = new JobQueuePrefetcher(_client, Polling(1, TimeSpan.FromSeconds(30)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var workers = Enumerable.Range(0, 5).Select(_ => FetchInBackground(prefetcher, cts.Token)).ToArray();
            var results = await Task.WhenAll(workers);

            results.Count(x => x is InvalidOperationException).Should().Be(1);
            results.Count(x => x is OperationCanceledException).Should().Be(4);
            _client.ReceivedWithAnyArgs(1).Fetch(default, default, default);
        }

        [Test]
        public void CancelledTokensNeverHitTheDatabase()
        {
            var prefetcher = new JobQueuePrefetcher(_client, _settings);
            prefetcher.Invoking(x => x.Fetch(["bulk"], new CancellationToken(true)))
                .Should().Throw<OperationCanceledException>();
            _client.DidNotReceiveWithAnyArgs().Fetch(default, default, default);
        }
    }

    public class PostgresJobQueueClientTests
    {
        private static readonly string _sql = PostgresJobQueueClient.FetchSql("hangfire");

        [Test]
        public void SeveralJobsAreFetchedPerRoundTripWithoutBlockingOtherWorkers()
        {
            _sql.Should().Contain(@"UPDATE ""hangfire"".""jobqueue""")
                .And.Contain("FOR UPDATE SKIP LOCKED")
                .And.Contain("LIMIT @limit")
                .And.Contain(@"RETURNING ""id"", ""jobid"", ""fetchedat""");
        }

        [Test]
        public void OnlyInvisibleOrUnfetchedEntriesAreTaken()
        {
            _sql.Should().Contain(@"""fetchedat"" IS NULL OR ""fetchedat"" < NOW() - @timeout")
                .And.Contain(@"""queue"" = ANY (@queues)");
        }

        [Test]
        public void EveryMutationIsScopedToTheFetchWeOwn()
        {
            PostgresJobQueueClient.RemoveSql("hangfire").Should()
                .Contain(@"""id"" = @id AND ""fetchedat"" = @fetchedat");
            PostgresJobQueueClient.RequeueSql("hangfire").Should()
                .Contain(@"""id"" = @id AND ""fetchedat"" = @fetchedat");
            PostgresJobQueueClient.KeepAliveSql("hangfire").Should()
                .Contain(@"""id"" = @id AND ""fetchedat"" = @fetchedat")
                .And.Contain(@"RETURNING ""fetchedat""");
            PostgresJobQueueClient.KeepAliveManySql("hangfire").Should()
                .Contain(@"q.""fetchedat"" = i.""fetchedat""")
                .And.Contain(@"RETURNING q.""id"", q.""fetchedat""");
        }

        [Test]
        public void SchemaNamesThatWouldBreakOutOfTheIdentifierAreRejected()
        {
            var act = () => new PostgresJobQueueClient(() => null, @"pub""lic");
            act.Should().Throw<ArgumentException>().WithMessage("*pub*");
        }

        [Test]
        public void SchemaIsHonoured()
        {
            PostgresJobQueueClient.FetchSql("other").Should().Contain(@"""other"".""jobqueue""")
                .And.NotContain(@"""hangfire""");
        }
    }
}
