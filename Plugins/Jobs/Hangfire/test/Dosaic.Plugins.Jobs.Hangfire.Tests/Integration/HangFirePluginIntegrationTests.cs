using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class HangFirePluginIntegrationTests : PostgresIntegrationTestBase
    {
        private HangfireConfiguration GetConfiguration() => new()
        {
            Host = DatabaseHost,
            Port = DatabasePort,
            Database = DatabaseName,
            User = DatabaseUser,
            Password = DatabasePassword,
            InMemory = false,
            SchemaName = SchemaName,
            AllowedDashboardHost = "localhost",
            WorkerCount = 4,
            QueuePollIntervalInMs = 200,
            Queues = ["default"],
            QueueConfigurations =
            [
                new QueueConfiguration { Name = "plugin-bulk", WorkerCount = 8, PrefetchCount = 20 }
            ]
        };

        private static ServiceProvider Build(HangFirePlugin plugin)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            plugin.ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        [Test]
        public async Task PluginWiresUpRealPostgresStorageWithPrefetchingAndBatching()
        {
            var plugin = new HangFirePlugin(GetConfiguration(), Substitute.For<IImplementationResolver>(), []);
            await using var provider = Build(plugin);

            provider.GetRequiredService<JobStorage>().Should().BeOfType<PrefetchJobStorage>();
            provider.GetRequiredService<IJobBatchDispatcher>().Should().BeOfType<PostgresJobBatchDispatcher>();

            var jobManager = provider.GetRequiredService<IJobManager>();
            var ids = await jobManager.EnqueueBatchAsync<RecordingJob, string>(
                Enumerable.Range(0, 30).Select(x => $"plugin-{x}"), "plugin-bulk");

            ids.Should().HaveCount(30).And.OnlyHaveUniqueItems();
            Storage.GetMonitoringApi().EnqueuedCount("plugin-bulk").Should().Be(30);
        }

        [Test]
        public async Task TheBulkDispatcherIsPickedEvenWhenItIsResolvedBeforeTheStorage()
        {
            var plugin = new HangFirePlugin(GetConfiguration(), Substitute.For<IImplementationResolver>(), []);
            await using var provider = Build(plugin);

            // resolving the dispatcher first is what has to force the Hangfire configuration callback to run
            provider.GetRequiredService<IJobBatchDispatcher>().Should().BeOfType<PostgresJobBatchDispatcher>();
        }

        [Test]
        public async Task PluginConfiguredJobsRunThroughTheDedicatedPrefetchingServer()
        {
            var plugin = new HangFirePlugin(GetConfiguration(), Substitute.For<IImplementationResolver>(), []);
            await using var provider = Build(plugin);
            var jobManager = provider.GetRequiredService<IJobManager>();

            var batch = jobManager.CreateBatch();
            for (var i = 0; i < 20; i++)
                batch.Enqueue<RecordingJob, string>($"plugin-run-{i}", "plugin-run")
                    .ContinueWith<RecordingJob, string>($"plugin-run-child-{i}", "plugin-run");
            await batch.SaveAsync();

            var storage = provider.GetRequiredService<JobStorage>();
            using (new BackgroundJobServer(new BackgroundJobServerOptions
            {
                Queues = ["plugin-run"],
                WorkerCount = 8,
                ServerName = $"{Environment.MachineName}:{Environment.ProcessId}:plugin-run",
                SchedulePollingInterval = TimeSpan.FromMilliseconds(200)
            }, storage))
                await WaitUntilAsync(
                    () => RecordingJob.Executed.Count(x => x.StartsWith("plugin-run-", StringComparison.Ordinal)) >= 40,
                    TimeSpan.FromMinutes(2), "all 20 roots and their continuations ran");

            Storage.GetMonitoringApi().EnqueuedCount("plugin-run").Should().Be(0);
        }

        [Test]
        public void PluginRegistersOneServerPerConfiguredQueueAgainstRealStorage()
        {
            var plugin = new HangFirePlugin(GetConfiguration(), Substitute.For<IImplementationResolver>(), []);
            using var provider = Build(plugin);

            provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                .OfType<BackgroundJobServerHostedService>().Should().HaveCount(2);
        }
    }
}
