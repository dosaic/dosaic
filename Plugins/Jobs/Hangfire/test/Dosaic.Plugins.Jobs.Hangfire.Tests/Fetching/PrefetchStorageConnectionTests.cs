using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using Hangfire.Storage;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Fetching
{
    public class PrefetchStorageConnectionTests
    {
        private JobStorageConnection _inner;

        [SetUp]
        public void Up() => _inner = Substitute.For<JobStorageConnection>();

        [Test]
        public void FetchNextJobIsServedByThePrefetcher()
        {
            var fetched = Substitute.For<IFetchedJob>();
            string[] seenQueues = null;
            var connection = new PrefetchStorageConnection(_inner, (queues, _) =>
            {
                seenQueues = queues;
                return fetched;
            });

            connection.FetchNextJob(["critical"], CancellationToken.None).Should().BeSameAs(fetched);
            seenQueues.Should().Equal("critical");
            _inner.DidNotReceive().FetchNextJob(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void EverythingElseIsDelegatedToTheWrappedConnection()
        {
            var connection = new PrefetchStorageConnection(_inner, (_, _) => null);
            var job = new global::Hangfire.Common.Job(typeof(TestJob), typeof(TestJob).GetMethod("ExecuteAsync"),
                new List<object> { CancellationToken.None });
            var hashEntries = new List<KeyValuePair<string, string>>();

            connection.CreateWriteTransaction();
            connection.AcquireDistributedLock("lock", TimeSpan.Zero);
            connection.CreateExpiredJob(job, new Dictionary<string, string>(), DateTime.UtcNow, TimeSpan.Zero);
            connection.SetJobParameter("1", "Queue", "critical");
            connection.GetJobParameter("1", "Queue");
            connection.GetJobData("1");
            connection.GetStateData("1");
            connection.AnnounceServer("server", null);
            connection.RemoveServer("server");
            connection.Heartbeat("server");
            connection.RemoveTimedOutServers(TimeSpan.Zero);
            connection.GetAllItemsFromSet("schedule");
            connection.GetFirstByLowestScoreFromSet("schedule", 0, 1);
            connection.GetFirstByLowestScoreFromSet("schedule", 0, 1, 5);
            connection.SetRangeInHash("hash", hashEntries);
            connection.GetAllEntriesFromHash("hash");
            connection.GetSetCount("schedule");
            connection.GetSetCount(["schedule"], 10);
            connection.GetSetContains("schedule", "1");
            connection.GetRangeFromSet("schedule", 0, 1);
            connection.GetSetTtl("schedule");
            connection.GetValueFromHash("hash", "name");
            connection.GetHashCount("hash");
            connection.GetHashTtl("hash");
            connection.GetListCount("list");
            connection.GetAllItemsFromList("list");
            connection.GetRangeFromList("list", 0, 1);
            connection.GetListTtl("list");
            connection.GetCounter("counter");
            connection.GetUtcDateTime();
            connection.Dispose();

            _inner.Received(1).CreateWriteTransaction();
            _inner.Received(1).AcquireDistributedLock("lock", TimeSpan.Zero);
            _inner.Received(1).CreateExpiredJob(job, Arg.Any<IDictionary<string, string>>(), Arg.Any<DateTime>(), TimeSpan.Zero);
            _inner.Received(1).SetJobParameter("1", "Queue", "critical");
            _inner.Received(1).GetJobParameter("1", "Queue");
            _inner.Received(1).GetJobData("1");
            _inner.Received(1).GetStateData("1");
            _inner.Received(1).AnnounceServer("server", null);
            _inner.Received(1).RemoveServer("server");
            _inner.Received(1).Heartbeat("server");
            _inner.Received(1).RemoveTimedOutServers(TimeSpan.Zero);
            _inner.Received(1).GetAllItemsFromSet("schedule");
            _inner.Received(1).GetFirstByLowestScoreFromSet("schedule", 0, 1);
            _inner.Received(1).GetFirstByLowestScoreFromSet("schedule", 0, 1, 5);
            _inner.Received(1).SetRangeInHash("hash", hashEntries);
            _inner.Received(1).GetAllEntriesFromHash("hash");
            _inner.Received(1).GetSetCount("schedule");
            _inner.Received(1).GetSetCount(Arg.Any<IEnumerable<string>>(), 10);
            _inner.Received(1).GetSetContains("schedule", "1");
            _inner.Received(1).GetRangeFromSet("schedule", 0, 1);
            _inner.Received(1).GetSetTtl("schedule");
            _inner.Received(1).GetValueFromHash("hash", "name");
            _inner.Received(1).GetHashCount("hash");
            _inner.Received(1).GetHashTtl("hash");
            _inner.Received(1).GetListCount("list");
            _inner.Received(1).GetAllItemsFromList("list");
            _inner.Received(1).GetRangeFromList("list", 0, 1);
            _inner.Received(1).GetListTtl("list");
            _inner.Received(1).GetCounter("counter");
            _inner.Received(1).GetUtcDateTime();
            _inner.Received(1).Dispose();
        }
    }
}
