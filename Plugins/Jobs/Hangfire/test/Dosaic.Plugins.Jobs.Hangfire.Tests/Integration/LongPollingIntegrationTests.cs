using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Npgsql;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class LongPollingIntegrationTests : PostgresIntegrationTestBase
    {
        // far longer than the tests wait, so a job that runs in time was woken up and not polled
        private static readonly TimeSpan _fallbackPollInterval = TimeSpan.FromSeconds(60);

        private static List<string> ExecutedWithPrefix(string prefix) =>
            RecordingJob.Executed.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        private async Task<NpgsqlConnection> ListenAsync()
        {
            var listener = CreateConnection();
            await listener.OpenAsync();
            await using var listen = new NpgsqlCommand(
                $"LISTEN {PostgresJobBatchDispatcher.NewJobNotificationChannel}", listener);
            await listen.ExecuteNonQueryAsync();
            return listener;
        }

        private PostgreSqlStorage LongPollingStorage()
        {
            var options = new PostgreSqlStorageOptions
            {
                SchemaName = SchemaName,
                PrepareSchemaIfNecessary = false,
                QueuePollInterval = _fallbackPollInterval,
                EnableLongPolling = true
            };
            return new PostgreSqlStorage(new NpgsqlConnectionFactory(ConnectionString, options), options);
        }

        [Test]
        public async Task ABatchNotifiesLongPollingWorkersOnCommit()
        {
            await using var listener = await ListenAsync();
            var batch = new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0, true));
            batch.Enqueue<RecordingJob, string>("notify-1", "notify");

            await batch.SaveAsync();

            (await listener.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue("the batch sent NOTIFY new_job");
        }

        [Test]
        public async Task ABatchDoesNotNotifyWithoutLongPolling()
        {
            await using var listener = await ListenAsync();
            var batch = new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0));
            batch.Enqueue<RecordingJob, string>("silent-1", "silent");

            await batch.SaveAsync();

            (await listener.WaitAsync(TimeSpan.FromMilliseconds(500))).Should().BeFalse();
        }

        [Test]
        public async Task IdleWorkersPickUpBatchedJobsWithoutWaitingForThePollInterval()
        {
            var storage = LongPollingStorage();
            using var server = new BackgroundJobServer(new BackgroundJobServerOptions
            {
                Queues = ["longpoll"],
                WorkerCount = 2,
                ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:longpoll",
                HeartbeatInterval = TimeSpan.FromSeconds(5)
            }, storage);
            // let the workers run their first, empty fetch and start waiting
            await Task.Delay(TimeSpan.FromSeconds(2));

            var stopwatch = Stopwatch.StartNew();
            var batch = new JobBatch(new PostgresJobBatchDispatcher(CreateConnection, SchemaName, 0, true));
            batch.Enqueue<RecordingJob, string>("longpoll-1", "longpoll");
            await batch.SaveAsync();
            await WaitUntilAsync(() => ExecutedWithPrefix("longpoll-").Count >= 1, TimeSpan.FromSeconds(20),
                "the notification woke an idle worker");

            stopwatch.Elapsed.Should().BeLessThan(_fallbackPollInterval);
        }
    }
}
