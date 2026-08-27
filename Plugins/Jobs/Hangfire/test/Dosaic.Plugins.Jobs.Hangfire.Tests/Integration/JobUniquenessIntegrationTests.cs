using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class JobUniquenessIntegrationTests : PostgresIntegrationTestBase
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
        private static readonly string _setKey = JobFingerprint.SetKey(UniqueRecordingJob.UniqueQueue);

        private IJobBatch NewBatch() =>
            new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0));

        [SetUp]
        public void RegisterStore() =>
            JobUniquenessStores.Use(Storage, new PostgresJobUniquenessStore(CreateConnection, SchemaName));

        private Task<long> ClaimCountAsync(string payload) =>
            ScalarAsync<long>($"""
                SELECT COUNT(*) FROM "{SchemaName}"."set"
                WHERE "key" = '{_setKey}' AND "value" = '{Fingerprint(payload)}';
                """);

        private static string Fingerprint(string payload) =>
            JobFingerprint.Compute(
                global::Hangfire.Common.Job.FromExpression<UniqueRecordingJob>(x =>
                    x.ExecuteAsync(payload, CancellationToken.None)),
                UniqueRecordingJob.UniqueQueue);

        [Test]
        public async Task DuplicatesInsideOneBatchAreWrittenOnce()
        {
            var batch = NewBatch();
            batch.Enqueue<UniqueRecordingJob, string>("dup");
            batch.Enqueue<UniqueRecordingJob, string>("dup");
            batch.Enqueue<UniqueRecordingJob, string>("other");
            var ids = await batch.SaveAsync();

            ids[0].Should().NotBeNull();
            ids[1].Should().BeNull("an earlier entry of the same batch already claims that fingerprint");
            ids[2].Should().NotBeNull();
            (await ClaimCountAsync("dup")).Should().Be(1);
            (await ClaimCountAsync("other")).Should().Be(1);
        }

        [Test]
        public async Task ADuplicateOfAnAlreadyQueuedJobIsNotWritten()
        {
            var first = NewBatch();
            first.Enqueue<UniqueRecordingJob, string>("across");
            var firstIds = await first.SaveAsync();

            var second = NewBatch();
            second.Enqueue<UniqueRecordingJob, string>("across");
            var secondIds = await second.SaveAsync();

            firstIds[0].Should().NotBeNull();
            secondIds[0].Should().BeNull();
            (await ClaimCountAsync("across")).Should().Be(1);
        }

        [Test]
        public async Task TheClaimIsTakenByTheSameStatementThatWritesTheJobs()
        {
            await ResetStatementCountsAsync();
            var batch = NewBatch();
            for (var i = 0; i < 100; i++)
                batch.Enqueue<UniqueRecordingJob, string>($"single-statement-{i}");
            var ids = await batch.SaveAsync();

            ids.Should().HaveCount(100).And.NotContainNulls();
            (await CountExecutedStatementsAsync(@"WITH ""input"" AS")).Should()
                .Be(1, "claiming 100 fingerprints must not cost 100 round trips");
        }

        [Test]
        public async Task ContinuationsOfASuppressedJobAreSuppressedAsWell()
        {
            var first = NewBatch();
            first.Enqueue<UniqueRecordingJob, string>("cascade");
            await first.SaveAsync();

            var before = await ScalarAsync<long>($"""SELECT COUNT(*) FROM "{SchemaName}"."job";""");
            var second = NewBatch();
            second.Enqueue<UniqueRecordingJob, string>("cascade")
                .ContinueWith<RecordingJob, string>("cascade-child", "cascade");
            var ids = await second.SaveAsync();

            ids.Should().AllSatisfy(x => x.Should().BeNull());
            (await ScalarAsync<long>($"""SELECT COUNT(*) FROM "{SchemaName}"."job";""")).Should().Be(before);
        }

        [Test]
        public async Task AnExpiredClaimIsTakenOverByTheNextJob()
        {
            var first = NewBatch();
            first.Enqueue<UniqueRecordingJob, string>("expired");
            await first.SaveAsync();
            await ExecuteAsync($"""
                UPDATE "{SchemaName}"."set" SET "score" = 0, "expireat" = to_timestamp(0)
                WHERE "key" = '{_setKey}' AND "value" = '{Fingerprint("expired")}';
                """);

            var second = NewBatch();
            second.Enqueue<UniqueRecordingJob, string>("expired");
            var ids = await second.SaveAsync();

            ids[0].Should().NotBeNull("a claim nobody released must not block the fingerprint forever");
            (await ClaimCountAsync("expired")).Should().Be(1);
        }

        [Test]
        public async Task TheClaimSurvivesInTheHangfireSetAndCarriesAnExpiration()
        {
            var batch = NewBatch();
            batch.Enqueue<UniqueRecordingJob, string>("expiring");
            await batch.SaveAsync();

            var expiring = await ScalarAsync<long>($"""
                SELECT COUNT(*) FROM "{SchemaName}"."set"
                WHERE "key" = '{_setKey}' AND "value" = '{Fingerprint("expiring")}'
                  AND "expireat" > NOW() AND "score" > 0;
                """);
            expiring.Should().Be(1);
        }

        [Test]
        public async Task TheClaimIsReleasedOnceTheJobIsPickedUp()
        {
            var batch = NewBatch();
            batch.Enqueue<UniqueRecordingJob, string>("released");
            await batch.SaveAsync();
            (await ClaimCountAsync("released")).Should().Be(1);

            using (StartServer())
                await WaitUntilAsync(() => UniqueRecordingJob.Executed.Contains("released"), _timeout,
                    "the unique job ran");
            await WaitUntilAsync(() => ClaimCountAsync("released").GetAwaiter().GetResult() == 0, _timeout,
                "the claim was released");

            var again = NewBatch();
            again.Enqueue<UniqueRecordingJob, string>("released");
            (await again.SaveAsync())[0].Should().NotBeNull("the fingerprint is free again");
        }

        [Test]
        public async Task TheFilterDeletesDuplicatesCreatedThroughTheRegularClient()
        {
            var client = new BackgroundJobClient(Storage);
            var first = client.Create(
                global::Hangfire.Common.Job.FromExpression<UniqueRecordingJob>(x =>
                    x.ExecuteAsync("client", CancellationToken.None)),
                new global::Hangfire.States.EnqueuedState());
            var second = client.Create(
                global::Hangfire.Common.Job.FromExpression<UniqueRecordingJob>(x =>
                    x.ExecuteAsync("client", CancellationToken.None)),
                new global::Hangfire.States.EnqueuedState());

            using var connection = Storage.GetConnection();
            connection.GetJobData(first).State.Should().Be("Enqueued");
            connection.GetJobData(second).State.Should().Be("Deleted");
            (await ClaimCountAsync("client")).Should().Be(1);
        }

        [Test]
        public async Task TheFilterAndTheBatchAgreeOnTheSameFingerprint()
        {
            new BackgroundJobClient(Storage).Create(
                global::Hangfire.Common.Job.FromExpression<UniqueRecordingJob>(x =>
                    x.ExecuteAsync("shared", CancellationToken.None)),
                new global::Hangfire.States.EnqueuedState());

            var batch = NewBatch();
            batch.Enqueue<UniqueRecordingJob, string>("shared");
            (await batch.SaveAsync())[0].Should()
                .BeNull("the client and the batch must compute the same fingerprint");
        }

        private BackgroundJobServer StartServer() =>
            new(new BackgroundJobServerOptions
            {
                Queues = [UniqueRecordingJob.UniqueQueue],
                WorkerCount = 1,
                ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:unique",
                SchedulePollingInterval = TimeSpan.FromMilliseconds(200),
                HeartbeatInterval = TimeSpan.FromSeconds(5)
            }, Storage);
    }
}
