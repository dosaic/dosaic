using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Hangfire;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class JobBatchingIntegrationTests : PostgresIntegrationTestBase
    {
        private IJobBatch NewBatch() =>
            new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0));

        [Test]
        public async Task ABatchOfThousandJobsCostsExactlyOneStatement()
        {
            var mark = await MarkLogAsync();
            var before = await ScalarAsync<long>($"""SELECT COUNT(*) FROM "{SchemaName}"."job";""");

            var batch = NewBatch();
            for (var i = 0; i < 1000; i++)
                batch.Enqueue<RecordingJob, string>($"bulk-{i}", "roundtrip");
            var ids = await batch.SaveAsync();

            ids.Should().HaveCount(1000).And.OnlyHaveUniqueItems();
            var after = await ScalarAsync<long>($"""SELECT COUNT(*) FROM "{SchemaName}"."job";""");
            (after - before).Should().Be(1000);
            var statements = await CountExecutedStatementsAsync(mark, @"WITH ""input"" AS");
            statements.Should().Be(1, "the whole batch must be written in a single round trip");
        }

        [Test]
        public async Task EnqueuedJobsAreIndistinguishableFromJobsCreatedByHangfire()
        {
            var reference = new BackgroundJobClient(Storage).Enqueue<RecordingJob>("reference",
                x => x.ExecuteAsync("reference-payload", CancellationToken.None));

            var batch = NewBatch();
            batch.Enqueue<RecordingJob, string>("batched-payload", "reference");
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            var referenceData = connection.GetJobData(reference);
            var batchedData = connection.GetJobData(ids[0]);

            batchedData.State.Should().Be(referenceData.State).And.Be("Enqueued");
            batchedData.Job.Type.Should().Be(referenceData.Job.Type).And.Be(typeof(RecordingJob));
            batchedData.Job.Method.Name.Should().Be(referenceData.Job.Method.Name);
            batchedData.Job.Args[0].Should().Be("batched-payload");
            connection.GetJobParameter(ids[0], "CurrentCulture").Should()
                .Be(connection.GetJobParameter(reference, "CurrentCulture"));
            Storage.GetMonitoringApi().EnqueuedCount("reference").Should().Be(2);
        }

        [Test]
        public async Task ScheduledJobsUseTheSameScheduleSetEntryAsHangfire()
        {
            var enqueueAt = DateTimeOffset.UtcNow.AddHours(1);
            var reference = new BackgroundJobClient(Storage)
                .Schedule<RecordingJob>("scheduled", x => x.ExecuteAsync("reference", CancellationToken.None),
                    enqueueAt);

            var batch = NewBatch();
            batch.ScheduleAt<RecordingJob, string>("batched", enqueueAt, "scheduled");
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            var scheduled = connection.GetAllItemsFromSet("schedule");
            scheduled.Should().Contain($"scheduled:{reference}").And.Contain($"scheduled:{ids[0]}");
            var stateData = connection.GetStateData(ids[0]);
            stateData.Name.Should().Be("Scheduled");
            stateData.Data.Should().ContainKey("EnqueueAt").And.ContainKey("ScheduledAt");
        }

        [Test]
        public async Task ContinuationChainsAreLinkedWithoutAnExtraRoundTrip()
        {
            var batch = NewBatch();
            var root = batch.Enqueue<RecordingJob, string>("root", "chained");
            var child = root.ContinueWith<RecordingJob, string>("child", "chained");
            child.ContinueWith<RecordingJob, string>("grandchild", "chained",
                JobContinuationOptions.OnAnyFinishedState);
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            connection.GetStateData(ids[1]).Data["ParentId"].Should().Be(ids[0]);
            connection.GetStateData(ids[2]).Data["ParentId"].Should().Be(ids[1]);
            connection.GetStateData(ids[1]).Name.Should().Be("Awaiting");
            connection.GetAllItemsFromSet("awaiting").Should().Contain(ids[1]).And.Contain(ids[2]);

            AssertContinuations(connection.GetJobParameter(ids[0], "Continuations"), ids[1],
                JobContinuationOptions.OnlyOnSucceededState);
            AssertContinuations(connection.GetJobParameter(ids[1], "Continuations"), ids[2],
                JobContinuationOptions.OnAnyFinishedState);
        }

        private static void AssertContinuations(string value, string expectedJobId,
            JobContinuationOptions expectedOptions)
        {
            var continuations = JArray.Parse(value);
            continuations.Should().ContainSingle();
            continuations[0]["JobId"]!.Value<string>().Should().Be(expectedJobId);
            continuations[0]["Options"]!.Value<int>().Should().Be((int)expectedOptions);
        }

        [Test]
        public async Task ContinuationsWrittenByTheBatchAreReadableByHangfire()
        {
            var batch = NewBatch();
            batch.Enqueue<RecordingJob, string>("root", "readable")
                .ContinueWith<RecordingJob, string>("child", "readable");
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            var awaiting = connection.GetStateData(ids[1]);
            var nextState = awaiting.Data["NextState"];
            nextState.Should().Contain("Hangfire.States.EnqueuedState").And.Contain("readable");
            awaiting.Data["Options"].Should().Be(nameof(JobContinuationOptions.OnlyOnSucceededState));
        }

        [Test]
        public async Task OneJobCanFanOutIntoSeveralContinuationsInTheSameStatement()
        {
            var mark = await MarkLogAsync();
            var batch = NewBatch();
            var root = batch.Enqueue<RecordingJob, string>("fanout-root", "fanout");
            root.ContinueWith<RecordingJob, string>("fanout-a", "fanout");
            root.ContinueWith<RecordingJob, string>("fanout-b", "fanout", JobContinuationOptions.OnAnyFinishedState);
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            connection.GetStateData(ids[1]).Data["ParentId"].Should().Be(ids[0]);
            connection.GetStateData(ids[2]).Data["ParentId"].Should().Be(ids[0]);

            var continuations = JArray.Parse(connection.GetJobParameter(ids[0], "Continuations"));
            continuations.Should().HaveCount(2, "both continuations belong to the same antecedent");
            continuations.Select(x => x["JobId"]!.Value<string>()).Should().Equal(ids[1], ids[2]);
            continuations.Select(x => x["Options"]!.Value<int>()).Should()
                .Equal((int)JobContinuationOptions.OnlyOnSucceededState,
                    (int)JobContinuationOptions.OnAnyFinishedState);
            (await CountExecutedStatementsAsync(mark, @"WITH ""input"" AS")).Should().Be(1);
        }

        [Test]
        public async Task ScheduledJobsCanBeChainedInsideTheSameBatch()
        {
            var batch = NewBatch();
            batch.Schedule<RecordingJob, string>("sched-root", TimeSpan.FromHours(3), "sched-chain")
                .ContinueWith<RecordingJob, string>("sched-child", "sched-chain");
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            connection.GetJobData(ids[0]).State.Should().Be("Scheduled");
            connection.GetJobData(ids[1]).State.Should().Be("Awaiting");
            connection.GetAllItemsFromSet("schedule").Should().Contain($"sched-chain:{ids[0]}");
            connection.GetAllItemsFromSet("awaiting").Should().Contain(ids[1]);
            connection.GetStateData(ids[1]).Data["ParentId"].Should().Be(ids[0]);
        }

        [Test]
        public async Task MixedBatchesWriteEveryStateInOneStatement()
        {
            var mark = await MarkLogAsync();
            var batch = NewBatch();
            batch.Enqueue<NoopJob>("mixed");
            batch.Schedule<RecordingJob, string>("later", TimeSpan.FromHours(2), "mixed");
            batch.Enqueue<RecordingJob, string>("chained-root", "mixed")
                .ContinueWith<RecordingJob, string>("chained-child", "mixed");
            var ids = await batch.SaveAsync();

            using var connection = Storage.GetConnection();
            connection.GetJobData(ids[0]).State.Should().Be("Enqueued");
            connection.GetJobData(ids[1]).State.Should().Be("Scheduled");
            connection.GetJobData(ids[2]).State.Should().Be("Enqueued");
            connection.GetJobData(ids[3]).State.Should().Be("Awaiting");
            (await CountExecutedStatementsAsync(mark, @"WITH ""input"" AS")).Should().Be(1);
        }

        [Test]
        public async Task ChunkingSplitsIntoSeveralStatementsButKeepsChainsIntact()
        {
            var mark = await MarkLogAsync();
            var batch = new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 2));
            batch.Enqueue<RecordingJob, string>("a", "chunked")
                .ContinueWith<RecordingJob, string>("b", "chunked")
                .ContinueWith<RecordingJob, string>("c", "chunked");
            batch.Enqueue<RecordingJob, string>("d", "chunked");
            batch.Enqueue<RecordingJob, string>("e", "chunked");
            var ids = await batch.SaveAsync();

            ids.Should().HaveCount(5).And.OnlyHaveUniqueItems();
            using var connection = Storage.GetConnection();
            connection.GetStateData(ids[1]).Data["ParentId"].Should().Be(ids[0]);
            connection.GetStateData(ids[2]).Data["ParentId"].Should().Be(ids[1]);
            (await CountExecutedStatementsAsync(mark, @"WITH ""input"" AS")).Should().Be(2);
        }
    }
}
