using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using Hangfire;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class JobExecutionIntegrationTests : PostgresIntegrationTestBase
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);

        private IJobBatch NewBatch() =>
            new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0));

        private PostgresJobQueueClient QueueClient() => new(CreateConnection, SchemaName);

        private static BackgroundJobServer StartServer(JobStorage storage, string queue, int workerCount) =>
            new(new BackgroundJobServerOptions
            {
                Queues = [queue],
                WorkerCount = workerCount,
                ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:{queue}",
                SchedulePollingInterval = TimeSpan.FromMilliseconds(200),
                HeartbeatInterval = TimeSpan.FromSeconds(5)
            }, storage);

        private static List<string> ExecutedWithPrefix(string prefix) =>
            RecordingJob.Executed.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        [Test]
        public async Task BatchedJobsAreExecutedAndTheirContinuationsFireInOrder()
        {
            var batch = NewBatch();
            for (var i = 0; i < 5; i++)
                batch.Enqueue<RecordingJob, string>($"e2e-root-{i}", "e2e")
                    .ContinueWith<RecordingJob, string>($"e2e-child-{i}", "e2e");
            await batch.SaveAsync();

            using (StartServer(Storage, "e2e", 5))
                await WaitUntilAsync(() => ExecutedWithPrefix("e2e-").Count >= 10, _timeout,
                    "all 5 roots and their 5 continuations ran");

            var executed = ExecutedWithPrefix("e2e-");
            for (var i = 0; i < 5; i++)
            {
                executed.Should().Contain($"e2e-root-{i}").And.Contain($"e2e-child-{i}");
                executed.IndexOf($"e2e-child-{i}").Should().BeGreaterThan(executed.IndexOf($"e2e-root-{i}"),
                    "a continuation only runs after its antecedent succeeded");
            }

            Storage.GetMonitoringApi().EnqueuedCount("e2e").Should().Be(0);
            Storage.GetMonitoringApi().SucceededListCount().Should().BeGreaterThanOrEqualTo(10);
        }

        [Test]
        public async Task FanOutContinuationsAllRunAfterTheirSharedAntecedent()
        {
            var batch = NewBatch();
            var root = batch.Enqueue<RecordingJob, string>("fan-root", "fan");
            root.ContinueWith<RecordingJob, string>("fan-a", "fan");
            root.ContinueWith<RecordingJob, string>("fan-b", "fan");
            await batch.SaveAsync();

            using (StartServer(Storage, "fan", 3))
                await WaitUntilAsync(() => ExecutedWithPrefix("fan-").Count >= 3, _timeout,
                    "the root and both of its continuations ran");

            var executed = ExecutedWithPrefix("fan-");
            executed.Should().Contain("fan-root").And.Contain("fan-a").And.Contain("fan-b");
            executed.IndexOf("fan-a").Should().BeGreaterThan(executed.IndexOf("fan-root"));
            executed.IndexOf("fan-b").Should().BeGreaterThan(executed.IndexOf("fan-root"));
        }

        [Test]
        public async Task ScheduledJobsAndTheirChainsRunOnceTheSchedulerPicksThemUp()
        {
            var batch = NewBatch();
            batch.Schedule<RecordingJob, string>("sched-root", TimeSpan.FromSeconds(1), "sched")
                .ContinueWith<RecordingJob, string>("sched-child", "sched");
            batch.Enqueue<RecordingJob, string>("sched-now", "sched");
            await batch.SaveAsync();

            using (StartServer(Storage, "sched", 3))
                await WaitUntilAsync(() => ExecutedWithPrefix("sched-").Count >= 3, _timeout,
                    "the enqueued job, the scheduled job and its continuation ran");

            var executed = ExecutedWithPrefix("sched-");
            executed.Should().Contain("sched-now").And.Contain("sched-root").And.Contain("sched-child");
            executed.IndexOf("sched-child").Should().BeGreaterThan(executed.IndexOf("sched-root"));
        }

        [Test]
        public async Task PrefetchingDrainsAQueueWithFarFewerRoundTripsThanJobs()
        {
            var batch = NewBatch();
            for (var i = 0; i < 25; i++)
                batch.Enqueue<RecordingJob, string>($"prefetch-{i}", "prefetch");
            await batch.SaveAsync();

            var roundTrips = 0;
            var client = new PostgresJobQueueClient(() =>
            {
                Interlocked.Increment(ref roundTrips);
                return CreateConnection();
            }, SchemaName);

            var first = client.Fetch(["prefetch"], 10, TimeSpan.FromMinutes(30));
            var second = client.Fetch(["prefetch"], 10, TimeSpan.FromMinutes(30));
            var third = client.Fetch(["prefetch"], 10, TimeSpan.FromMinutes(30));

            first.Should().HaveCount(10);
            second.Should().HaveCount(10);
            third.Should().HaveCount(5);
            first.Select(x => x.JobId).Should().NotIntersectWith(second.Select(x => x.JobId));
            second.Select(x => x.JobId).Should().NotIntersectWith(third.Select(x => x.JobId));
            roundTrips.Should().Be(3, "25 jobs were fetched with 3 round trips instead of 25");
        }

        [Test]
        public async Task RemovingAndRequeueingAPrefetchedJobBehavesLikeHangfire()
        {
            var batch = NewBatch();
            batch.Enqueue<RecordingJob, string>("requeue-a", "requeue");
            batch.Enqueue<RecordingJob, string>("requeue-b", "requeue");
            await batch.SaveAsync();

            var client = QueueClient();
            var fetched = client.Fetch(["requeue"], 2, TimeSpan.FromMinutes(30));
            fetched.Should().HaveCount(2);
            Storage.GetMonitoringApi().EnqueuedCount("requeue").Should().Be(0);

            var renewed = client.KeepAlive(fetched[1].QueueEntryId, fetched[1].FetchedAt);
            renewed.Should().NotBeNull();
            client.KeepAlive(fetched[1].QueueEntryId, fetched[1].FetchedAt).Should()
                .BeNull("the old fetch timestamp no longer matches");
            fetched[1].FetchedAt = renewed.Value;
            Storage.GetMonitoringApi().FetchedCount("requeue").Should().Be(2,
                "keeping a job alive must not make it visible again");

            client.Remove(fetched[0].QueueEntryId, fetched[0].FetchedAt);
            client.Requeue(fetched[1].QueueEntryId, fetched[1].FetchedAt);

            Storage.GetMonitoringApi().EnqueuedCount("requeue").Should().Be(1);
            var again = client.Fetch(["requeue"], 5, TimeSpan.FromMinutes(30));
            again.Should().ContainSingle().Which.QueueEntryId.Should().Be(fetched[1].QueueEntryId);
        }

        [Test]
        public async Task PrefetchingStorageRunsAWholeBatchEndToEnd()
        {
            var batch = NewBatch();
            for (var i = 0; i < 50; i++)
                batch.Enqueue<RecordingJob, string>($"prefetch-e2e-{i}", "prefetch-e2e");
            await batch.SaveAsync();

            var settings = new PrefetchSettings
            {
                PrefetchCount = 25,
                PollInterval = TimeSpan.FromMilliseconds(200),
                InvisibilityTimeout = TimeSpan.FromMinutes(30)
            };
            var storage = new PrefetchJobStorage(Storage, QueueClient(),
                new Dictionary<string, PrefetchSettings> { ["prefetch-e2e"] = settings }, settings);

            using (StartServer(storage, "prefetch-e2e", 10))
                await WaitUntilAsync(() => ExecutedWithPrefix("prefetch-e2e-").Count >= 50, _timeout,
                    "all 50 prefetched jobs ran");

            ExecutedWithPrefix("prefetch-e2e-").Should().HaveCount(50).And.OnlyHaveUniqueItems();
            Storage.GetMonitoringApi().EnqueuedCount("prefetch-e2e").Should().Be(0);
            Storage.GetMonitoringApi().FetchedCount("prefetch-e2e").Should().Be(0);
        }
    }
}
