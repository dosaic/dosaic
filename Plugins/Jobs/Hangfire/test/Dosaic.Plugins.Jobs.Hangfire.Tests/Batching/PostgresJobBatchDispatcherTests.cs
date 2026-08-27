using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Hangfire;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Batching
{
    public class PostgresJobBatchDispatcherTests
    {
        private static readonly string _sql = PostgresJobBatchDispatcher.BuildSql("hangfire");

        [Test]
        public void EverythingIsWrittenBySingleStatement()
        {
            _sql.TrimEnd().Split(';', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
        }

        [Test]
        public void AllHangfireTablesAreWrittenWithTheConfiguredSchema()
        {
            _sql.Should().Contain("""INSERT INTO "hangfire"."job" """.TrimEnd())
                .And.Contain("""INSERT INTO "hangfire"."state" """.TrimEnd())
                .And.Contain("""INSERT INTO "hangfire"."jobqueue" """.TrimEnd())
                .And.Contain("""INSERT INTO "hangfire"."set" """.TrimEnd())
                .And.Contain("""INSERT INTO "hangfire"."jobparameter" """.TrimEnd());
            PostgresJobBatchDispatcher.BuildSql("other").Should().Contain(@"""other"".""job""")
                .And.NotContain(@"""hangfire""");
        }

        [Test]
        public void JobAndStateIdsArePreAllocatedFromTheSequences()
        {
            _sql.Should().Contain(@"nextval(pg_get_serial_sequence('""hangfire"".""job""', 'id')::regclass)")
                .And.Contain(@"nextval(pg_get_serial_sequence('""hangfire"".""state""', 'id')::regclass)");
        }

        [Test]
        public void ContinuationParentIdIsPatchedIntoTheAwaitingStateData()
        {
            _sql.Should().Contain("""jsonb_set("statedata"::jsonb, '{ParentId}', to_jsonb("parentjobid"::text))""");
        }

        [Test]
        public void ContinuationsAreAggregatedOntoTheAntecedentJob()
        {
            _sql.Should().Contain("'Continuations'")
                .And.Contain("""jsonb_build_object('JobId', c."jobid"::text, 'Options', c."continuationoptions")""");
        }

        [Test]
        public void ScheduledJobsAreStoredWithTheQueuePrefixHangfireExpects()
        {
            _sql.Should().Contain("""COALESCE("setprefix" || ':', '') || "jobid"::text""");
        }
        [Test]
        public async Task ParametersAreFlattenedIntoParallelArrays()
        {
            var batch = new JobBatch(Substitute.For<IJobBatchDispatcher>());
            batch.Enqueue<TestParamJob, string>("payload", "critical");
            batch.Schedule<TestJob>(TimeSpan.FromMinutes(5), "bulk");
            var entries = Capture(batch);

            var parameters = PostgresJobBatchDispatcher.BuildParameters(entries);

            parameters.StateNames.Should().Equal("Enqueued", "Scheduled");
            parameters.Queues.Should().Equal("critical", null);
            parameters.SetKeys.Should().Equal(null, "schedule");
            parameters.SetPrefixes.Should().Equal(null, "bulk");
            parameters.ParentIndexes.Should().AllSatisfy(x => x.Should().BeNull());
            parameters.InvocationData[0].Should().Contain(nameof(TestParamJob));
            parameters.Arguments[0].Should().Contain("payload");
            parameters.StateData[0].Should().Contain("critical");
            parameters.ParameterIndexes.Should().OnlyContain(x => x == 1 || x == 2);
            parameters.ParameterNames.Should().Contain("CurrentCulture");
            await Task.CompletedTask;
        }

        [Test]
        public void ContinuationIndexesAreRemappedToTheChunkLocalOrdinality()
        {
            var batch = new JobBatch(Substitute.For<IJobBatchDispatcher>());
            batch.Enqueue<TestJob>();
            batch.Enqueue<TestJob>().ContinueWith<TestJob>();
            var entries = Capture(batch);

            var chunk = entries.Skip(1).ToList();
            var parameters = PostgresJobBatchDispatcher.BuildParameters(chunk);

            parameters.ParentIndexes.Should().Equal(null, 1);
            parameters.StateNames[1].Should().Be("Awaiting");
            parameters.StateData[1].Should().Contain("ParentId");
            parameters.ContinuationOptions[1].Should().Be((int)JobContinuationOptions.OnlyOnSucceededState);
        }

        private static IReadOnlyList<BatchJobEntry> Capture(IJobBatch batch)
        {
            var field = typeof(JobBatch).GetField("_entries",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (List<BatchJobEntry>)field.GetValue(batch);
        }
    }
}
