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
