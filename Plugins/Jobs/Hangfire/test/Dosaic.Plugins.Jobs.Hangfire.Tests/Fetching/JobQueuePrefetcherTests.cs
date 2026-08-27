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

        [SetUp]
        public void Up() => _client = Substitute.For<IJobQueueClient>();

        [Test]
        public void OneRoundTripServesTheWholePrefetchBuffer()
        {
            _client.Fetch(Arg.Any<string[]>(), 3, Arg.Any<TimeSpan>())
                .Returns([new PrefetchedQueueEntry(10, 100), new PrefetchedQueueEntry(11, 101), new PrefetchedQueueEntry(12, 102)],
                    []);
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
                .And.Contain(@"RETURNING ""id"", ""jobid""");
        }

        [Test]
        public void OnlyInvisibleOrUnfetchedEntriesAreTaken()
        {
            _sql.Should().Contain(@"""fetchedat"" IS NULL OR ""fetchedat"" < NOW() - @timeout")
                .And.Contain(@"""queue"" = ANY (@queues)");
        }

        [Test]
        public void SchemaIsHonoured()
        {
            PostgresJobQueueClient.FetchSql("other").Should().Contain(@"""other"".""jobqueue""")
                .And.NotContain(@"""hangfire""");
        }
    }
}
