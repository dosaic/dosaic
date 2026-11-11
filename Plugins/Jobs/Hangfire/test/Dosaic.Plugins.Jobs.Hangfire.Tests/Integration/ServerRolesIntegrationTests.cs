using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    /// <summary>
    ///     One image, several roles: a process that only enqueues, a process that only serves some queues and a
    ///     process that only prepares the schema. Every test works in a schema of its own, so that the servers
    ///     and recurring jobs of one role never show up in the assertions of another.
    /// </summary>
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public class ServerRolesIntegrationTests : PostgresIntegrationTestBase
    {
        private List<JobFilter> _filtersBefore;

        [SetUp]
        public void RememberGlobalFilters() => _filtersBefore = GlobalJobFilters.Filters.ToList();

        [TearDown]
        public void RemoveFiltersTheHostsAdded()
        {
            foreach (var filter in GlobalJobFilters.Filters.ToList()
                         .Where(x => _filtersBefore.All(before => !ReferenceEquals(before.Instance, x.Instance))))
                GlobalJobFilters.Filters.Remove(filter.Instance);
        }

        private HangfireConfiguration GetConfiguration(string schema) => new()
        {
            Host = DatabaseHost,
            Port = DatabasePort,
            Database = DatabaseName,
            User = DatabaseUser,
            Password = DatabasePassword,
            InMemory = false,
            SchemaName = schema,
            AllowedDashboardHost = "localhost",
            PollingIntervalInMs = 200,
            QueuePollIntervalInMs = 200,
            StatisticsMetrics = new HangfireStatisticsMetricsConfiguration { Enabled = false }
        };

        private Task<bool> SchemaExistsAsync(string schema) => ScalarAsync<bool>(
            $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = 'schema');");

        /// <summary>
        ///     Mirrors what the web host does with the plugin: services, health checks, build,
        ///     <see cref="HangFirePlugin.ConfigureApplication" /> and finally the hosted services.
        /// </summary>
        private sealed class RoleHost : IAsyncDisposable
        {
            private readonly HangFirePlugin _plugin;
            private readonly List<IHostedService> _started = [];
            public ServiceProvider Services { get; }

            public RoleHost(HangfireConfiguration configuration, Type[] discoveredJobs = null,
                Action<IServiceCollection> configure = null)
            {
                var resolver = Substitute.For<IImplementationResolver>();
                resolver.FindTypes().Returns((discoveredJobs ?? []).ToList());
                _plugin = new HangFirePlugin(configuration, resolver, []);
                var services = new ServiceCollection();
                services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
                services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                _plugin.ConfigureServices(services);
                _plugin.ConfigureHealthChecks(services.AddHealthChecks());
                configure?.Invoke(services);
                Services = services.BuildServiceProvider();
            }

            public JobStorage Storage => Services.GetRequiredService<JobStorage>();

            public async Task<RoleHost> StartAsync()
            {
                _plugin.ConfigureApplication(new ApplicationBuilder(Services));
                foreach (var hostedService in Services.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(CancellationToken.None);
                    _started.Add(hostedService);
                }

                return this;
            }

            public async Task<HealthStatus> GetHangfireHealthAsync()
            {
                var report = await Services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
                return report.Entries["hangfire"].Status;
            }

            public async ValueTask DisposeAsync()
            {
                foreach (var hostedService in _started)
                    await hostedService.StopAsync(CancellationToken.None);
                await Services.DisposeAsync();
            }
        }

        [Test]
        public async Task ProcessWithoutServerEnqueuesAndRegistersRecurringJobsAndIsReady()
        {
            var configuration = GetConfiguration("role_api");
            configuration.Server.Enabled = false;
            await using var host = await new RoleHost(configuration, [typeof(TestJobTimeoutAsync)]).StartAsync();

            host.Services.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>().Should().BeEmpty();
            // give a server that should not exist the time it would need to announce itself
            await Task.Delay(TimeSpan.FromSeconds(2));
            host.Storage.GetMonitoringApi().Servers().Should().BeEmpty();

            var jobId = host.Services.GetRequiredService<IJobManager>().Enqueue<NoopJob>();
            jobId.Should().NotBeNullOrEmpty();
            host.Storage.GetMonitoringApi().EnqueuedCount("default").Should().Be(1);

            var batchIds = await host.Services.GetRequiredService<IJobManager>()
                .EnqueueBatchAsync<RecordingJob, string>(["role-api-1", "role-api-2"], "import");
            batchIds.Should().HaveCount(2);
            host.Storage.GetMonitoringApi().EnqueuedCount("import").Should().Be(2);

            using var connection = host.Storage.GetConnection();
            connection.GetRecurringJobs().Should().ContainSingle()
                .Which.Job.Type.Should().Be(typeof(TestJobTimeoutAsync));

            (await host.GetHangfireHealthAsync()).Should().Be(HealthStatus.Healthy,
                "a process without a server must not wait for one to exist anywhere");
        }

        [Test]
        public async Task ProcessWithoutServerPreparesTheSchemaOnAnEmptyDatabaseWhenItResolvesTheStorage()
        {
            (await SchemaExistsAsync("role_migrate")).Should().BeFalse();
            var configuration = GetConfiguration("role_migrate");
            configuration.Server.Enabled = false;
            configuration.PrepareSchemaIfNecessary = true;
            configuration.RegisterRecurringJobs = false;
            await using var host = new RoleHost(configuration);

            host.Storage.Should().NotBeNull();

            (await SchemaExistsAsync("role_migrate")).Should().BeTrue();
            (await ScalarAsync<int>("""SELECT max("version"::integer) FROM "role_migrate"."schema";"""))
                .Should().Be(PostgresSchemaGuard.SupportedSchemaVersion);
        }

        [Test]
        public async Task ProcessThatMustNotPrepareTheSchemaFailsStartupWhenTheSchemaIsMissing()
        {
            var configuration = GetConfiguration("role_unprepared");
            configuration.PrepareSchemaIfNecessary = false;
            await using var host = new RoleHost(configuration);

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*schema was not prepared*");
            (await SchemaExistsAsync("role_unprepared")).Should().BeFalse();
        }

        [Test]
        public async Task ProcessThatMustNotPrepareTheSchemaStartsOnASchemaAnotherProcessPrepared()
        {
            var migrate = GetConfiguration("role_prepared");
            migrate.Server.Enabled = false;
            await using (var migrateHost = new RoleHost(migrate))
                migrateHost.Storage.Should().NotBeNull();

            var configuration = GetConfiguration("role_prepared");
            configuration.Server.Enabled = false;
            configuration.PrepareSchemaIfNecessary = false;
            await using var host = await new RoleHost(configuration).StartAsync();

            host.Services.GetRequiredService<IJobManager>().Enqueue<NoopJob>().Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task QueueMapServesOnlyTheListedQueueWithItsWorkerCount()
        {
            var configuration = GetConfiguration("role_worker");
            configuration.Queues = ["default", "other"];
            configuration.WorkerCount = 10;
            configuration.Server.Queues = new Dictionary<string, int> { ["IMPORT"] = 3, ["mail"] = 0 };
            await using var host = await new RoleHost(configuration).StartAsync();
            var monitoring = host.Storage.GetMonitoringApi();
            await WaitUntilAsync(() => monitoring.Servers().Count > 0, TimeSpan.FromSeconds(30),
                "the import server announced itself");

            var server = monitoring.Servers().Should().ContainSingle().Which;
            server.Queues.Should().Equal("import");
            server.WorkersCount.Should().Be(3);
            server.Name.Should().Contain(":import");
            (await host.GetHangfireHealthAsync()).Should().Be(HealthStatus.Healthy);

            var jobManager = host.Services.GetRequiredService<IJobManager>();
            jobManager.Enqueue<RecordingJob, string>("role-worker-default");
            jobManager.Enqueue<RecordingJob, string>("role-worker-import", "import");
            await WaitUntilAsync(() => RecordingJob.Executed.Contains("role-worker-import"), TimeSpan.FromSeconds(30),
                "the import job ran");
            // several poll intervals - a server polling default would have picked the job up by now
            await Task.Delay(TimeSpan.FromSeconds(2));

            RecordingJob.Executed.Should().NotContain("role-worker-default");
            monitoring.EnqueuedCount("default").Should().Be(1);
            monitoring.EnqueuedCount("import").Should().Be(0);
        }

        [Test]
        public async Task ProcessThatDoesNotRegisterRecurringJobsWritesNone()
        {
            var configuration = GetConfiguration("role_norecurring");
            configuration.Server.Enabled = false;
            configuration.RegisterRecurringJobs = false;
            var configured = false;
            await using var host = await new RoleHost(configuration, [typeof(TestJobTimeoutAsync)],
                services => services.AddSingleton(new JobOptions((_, _) => configured = true))).StartAsync();

            configured.Should().BeFalse();
            using var connection = host.Storage.GetConnection();
            connection.GetRecurringJobs().Should().BeEmpty();
            (await ScalarAsync<long>("""SELECT count(*) FROM "role_norecurring"."set" WHERE "key" = 'recurring-jobs';"""))
                .Should().Be(0);
        }
    }
}
