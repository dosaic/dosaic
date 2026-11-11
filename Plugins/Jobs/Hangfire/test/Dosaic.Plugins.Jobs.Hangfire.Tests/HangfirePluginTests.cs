using System.Reflection;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Testing.NUnit.Assertions;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    public class HangfirePluginTests
    {
        internal static HangfireConfiguration GetConfiguration() => new()
        {
            Database = "testDatabase",
            Host = "testHost",
            Port = 9999,
            Password = "testPassword",
            AllowedDashboardHost = "localhost",
            InMemory = true,
            PollingIntervalInMs = 1000,
            WorkerCount = 200,
            EnableJobsByFeatureManagementConfig = true,
            User = "testUser",
            Queues = ["default", "test"]
        };
        private HangfireConfiguration _hangfireConfiguration;

        private IImplementationResolver _implementationResolver;
        private IHangfireConfigurator _configurator;
        private HangFirePlugin GetPlugin() => new(_hangfireConfiguration, _implementationResolver, [_configurator]);

        [SetUp]
        public void Up()
        {
            _configurator = Substitute.For<IHangfireConfigurator>();
            _implementationResolver = Substitute.For<IImplementationResolver>();
            _hangfireConfiguration = GetConfiguration();
        }

        [Test]
        public void ConfigureServiceTest()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            GetPlugin().ConfigureServices(sc);

            var sp = sc.BuildServiceProvider();

            var hangfireConfiguration = sp.GetRequiredService<IGlobalConfiguration>();
            hangfireConfiguration.Should().NotBeNull();

            var backgroundJobServerOptions = sp.GetServices<IHostedService>();
            backgroundJobServerOptions.Should().NotBeNull();

            var jobManager = sp.GetService<IJobManager>();
            jobManager.Should().NotBeNull();
            jobManager.Should().BeOfType<JobManager>();

            _configurator.Received(1).Configure(Arg.Any<IGlobalConfiguration>());
            _configurator.Received(1).ConfigureServer(Arg.Any<BackgroundJobServerOptions>());
        }

        [Test]
        public void ConfigureApplicationTest()
        {
            _implementationResolver.FindTypes()
                .Returns([typeof(TestJobTimeoutAsync), typeof(GenericTestJobAsync<>)]);
            JobStorage.Current = new MemoryStorage();
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

            var plugin = GetPlugin();
            var featureManager = Substitute.For<IFeatureManager>();

            plugin.ConfigureServices(sc);
            sc.AddSingleton(featureManager);
            var serviceProvider = sc.BuildServiceProvider();
            var applicationBuilder = new ApplicationBuilder(serviceProvider);
            plugin.ConfigureApplication(applicationBuilder);

            GlobalJobFilters.Filters.Should().Contain(x => x.Instance.GetType() == typeof(EnabledByFeatureFilter));
            GlobalJobFilters.Filters.Should().Contain(x => x.Instance.GetType() == typeof(LogJobExecutionFilter));

            var recurringJobs = JobStorage.Current.GetConnection().GetRecurringJobs();
            recurringJobs.Should().HaveCount(1);
            recurringJobs[0].Job.Type.Should().Be(typeof(TestJobTimeoutAsync));
        }

        [Test]
        public void HangfireConfigureHealthChecksWorks()
        {
            var hcBuilder = Substitute.For<IHealthChecksBuilder>();
            var plugin = new HangFirePlugin(_hangfireConfiguration, _implementationResolver, []);
            plugin.ConfigureHealthChecks(hcBuilder);
            hcBuilder.Add(Arg.Is<HealthCheckRegistration>(h => h.Name == "hangfire" && h.Tags.Contains(HealthCheckTag.Readiness.Value)))
                .Received(Quantity.Exactly(1));
        }

        [Test]
        public void CanSpecifyServerOptions()
        {
            var sc = new ServiceCollection();
            sc.AddScoped<ILogger<HangfireStatisticsMetricsReporter>, NullLogger<HangfireStatisticsMetricsReporter>>();
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            var hangfireHostedService = sp.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>().First();
            hangfireHostedService.Should().NotBeNull();
            var property = typeof(BackgroundJobServerHostedService).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
            property.Should().NotBeNull();
            var options = (property.GetValue(hangfireHostedService) as BackgroundJobServerOptions)!;
            options.Should().NotBeNull();
            options.SchedulePollingInterval.Should().Be(TimeSpan.FromMilliseconds(_hangfireConfiguration.PollingIntervalInMs!.Value));
            options.WorkerCount.Should().Be(_hangfireConfiguration.WorkerCount!.Value);
        }

        [Test]
        public void InMemoryStorageFallsBackToTheClientBatchDispatcher()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

            sp.GetRequiredService<IJobBatchDispatcher>().Should()
                .BeOfType<BackgroundJobClientBatchDispatcher>("the bulk statement only works on our own PostgreSQL");
        }

        [Test, Parallelizable(ParallelScope.None)]
        public void ConfiguratorSuppliedStorageFallsBackToTheClientBatchDispatcher()
        {
            JobStorage.Current = null;
            _configurator.IncludesStorage.Returns(true);
            _configurator.When(x => x.Configure(Arg.Any<IGlobalConfiguration>()))
                .Do(x => x.Arg<IGlobalConfiguration>().UseMemoryStorage());
            _hangfireConfiguration.InMemory = false;
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

            sp.GetRequiredService<IJobBatchDispatcher>().Should().BeOfType<BackgroundJobClientBatchDispatcher>();
        }

        [Test]
        public void EachConfiguredQueueGetsItsOwnServerWithItsOwnWorkerCount()
        {
            _hangfireConfiguration.QueueConfigurations =
            [
                new QueueConfiguration { Name = "bulk", WorkerCount = 50 },
                new QueueConfiguration { Name = "critical" }
            ];
            var sc = new ServiceCollection();
            sc.AddScoped<ILogger<HangfireStatisticsMetricsReporter>, NullLogger<HangfireStatisticsMetricsReporter>>();
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

            var options = sp.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>()
                .Select(GetOptions).ToList();
            options.Should().HaveCount(3);
            var shared = options.Single(x => x.ServerName is null);
            shared.Queues.Should().BeEquivalentTo("default", "test");
            shared.WorkerCount.Should().Be(200);
            var bulk = options.Single(x => x.Queues.Contains("bulk"));
            bulk.Queues.Should().Equal("bulk");
            bulk.WorkerCount.Should().Be(50);
            bulk.ServerName.Should().EndWith(":bulk");
            var critical = options.Single(x => x.Queues.Contains("critical"));
            critical.WorkerCount.Should().Be(200);
        }

        [Test]
        public void QueuesWithoutAConfigurationStayOnTheSharedServer()
        {
            var sc = new ServiceCollection();
            sc.AddScoped<ILogger<HangfireStatisticsMetricsReporter>, NullLogger<HangfireStatisticsMetricsReporter>>();
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            sp.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>().Should().ContainSingle();
        }

        private static BackgroundJobServerOptions GetOptions(BackgroundJobServerHostedService service)
        {
            var field = typeof(BackgroundJobServerHostedService)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (BackgroundJobServerOptions)field.GetValue(service);
        }

        private List<BackgroundJobServerOptions> GetServerOptions()
        {
            var sc = new ServiceCollection();
            sc.AddScoped<ILogger<HangfireStatisticsMetricsReporter>, NullLogger<HangfireStatisticsMetricsReporter>>();
            GetPlugin().ConfigureServices(sc);
            return sc.BuildServiceProvider().GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>()
                .Select(GetOptions).ToList();
        }

        private static ApplicationBuilder ConfigureApplication(HangFirePlugin plugin, ILoggerFactory loggerFactory,
            Action<IServiceCollection> configure = null)
        {
            JobStorage.Current = new MemoryStorage();
            var sc = new ServiceCollection();
            sc.AddSingleton(loggerFactory);
            plugin.ConfigureServices(sc);
            sc.AddSingleton(Substitute.For<IFeatureManager>());
            configure?.Invoke(sc);
            var applicationBuilder = new ApplicationBuilder(sc.BuildServiceProvider());
            plugin.ConfigureApplication(applicationBuilder);
            return applicationBuilder;
        }

        [Test]
        public void DefaultConfigurationKeepsOneSharedServerOnTheDefaultQueue()
        {
            _hangfireConfiguration = new HangfireConfiguration { InMemory = true };

            var options = GetServerOptions();

            options.Should().ContainSingle();
            options[0].Queues.Should().Equal("default");
            options[0].ServerName.Should().BeNull();
            options[0].WorkerCount.Should().Be(new BackgroundJobServerOptions().WorkerCount);
            options[0].ShutdownTimeout.Should().Be(new BackgroundJobServerOptions().ShutdownTimeout);
        }

        [Test]
        public void DefaultConfigurationKeepsTheNewSwitchesOnTodaysBehaviour()
        {
            var configuration = new HangfireConfiguration();

            configuration.Server.Enabled.Should().BeTrue();
            configuration.Server.Queues.Should().BeEmpty();
            configuration.Server.ShutdownTimeoutInSeconds.Should().BeNull();
            configuration.PrepareSchemaIfNecessary.Should().BeTrue();
            configuration.RegisterRecurringJobs.Should().BeTrue();
            configuration.HealthCheck.Enabled.Should().BeTrue();
            configuration.HealthCheck.MinimumAvailableServers.Should().BeNull();
            configuration.StatisticsMetrics.Enabled.Should().BeTrue();
        }

        [Test]
        public void DisabledServerRegistersNoBackgroundJobServerButKeepsTheClients()
        {
            _hangfireConfiguration.Server.Enabled = false;
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

            sp.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>().Should().BeEmpty();
            sp.GetRequiredService<IJobManager>().Should().BeOfType<JobManager>();
            sp.GetRequiredService<IBackgroundJobClient>().Should().NotBeNull();
            sp.GetRequiredService<IRecurringJobManager>().Should().NotBeNull();
            sp.GetRequiredService<IJobBatchDispatcher>().Should().NotBeNull();
            _configurator.DidNotReceive().ConfigureServer(Arg.Any<BackgroundJobServerOptions>());
        }

        [Test]
        public void DisabledServerWinsOverTheQueueMap()
        {
            _hangfireConfiguration.Server.Enabled = false;
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = 3, ["not valid!"] = 1 };

            GetServerOptions().Should().BeEmpty();
        }

        [Test]
        public void ServerQueueMapGivesEveryQueueWithWorkersItsOwnServerAndNothingElse()
        {
            _hangfireConfiguration.QueueConfigurations = [new QueueConfiguration { Name = "import", WorkerCount = 50 }];
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int>
            {
                ["import"] = 3,
                ["export"] = 0,
                ["mail"] = 2
            };

            var options = GetServerOptions();

            options.Should().HaveCount(2);
            options.SelectMany(x => x.Queues).Should().BeEquivalentTo("import", "mail");
            var import = options.Single(x => x.Queues.Contains("import"));
            import.Queues.Should().Equal("import");
            import.WorkerCount.Should().Be(3);
            import.ServerName.Should().Be($"{Environment.MachineName}:{Environment.ProcessId}:import");
            import.SchedulePollingInterval.Should().Be(TimeSpan.FromMilliseconds(1000));
            var mail = options.Single(x => x.Queues.Contains("mail"));
            mail.WorkerCount.Should().Be(2);
            mail.ServerName.Should().EndWith(":mail");
        }

        [Test]
        public void ServerQueueMapKeysAreTrimmedAndLowerCased()
        {
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["IMPORT"] = 40, [" Mail-Out "] = 1 };

            var options = GetServerOptions();

            options.Select(x => x.Queues.Single()).Should().BeEquivalentTo("import", "mail-out");
            options.Single(x => x.Queues.Contains("import")).WorkerCount.Should().Be(40);
        }

        [TestCase("not valid!")]
        [TestCase("with.dot")]
        [TestCase("  ")]
        public void InvalidServerQueueNameThrowsAndNamesTheKey(string name)
        {
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { [name] = 1 };

            var act = () => GetPlugin().ConfigureServices(new ServiceCollection());

            act.Should().Throw<InvalidOperationException>().WithMessage($"*hangfire:server:queues:{name}*");
        }

        [Test]
        public void NegativeServerQueueWorkerCountThrowsAndNamesTheKey()
        {
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = -1 };

            var act = () => GetPlugin().ConfigureServices(new ServiceCollection());

            act.Should().Throw<InvalidOperationException>().WithMessage("*hangfire:server:queues:import*-1*");
        }

        [Test]
        public void ServerQueueNamedTwiceAfterNormalisationThrows()
        {
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = 1, ["IMPORT "] = 2 };

            var act = () => GetPlugin().ConfigureServices(new ServiceCollection());

            act.Should().Throw<InvalidOperationException>().WithMessage("*hangfire:server:queues:*'import'*");
        }

        [Test]
        public void ServerQueueMapWithOnlyZeroesStartsNoServerAndLogsAWarning()
        {
            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = 0, ["mail"] = 0 };
            var logger = new FakeLogger<HangFirePlugin>();
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

            _implementationResolver.FindTypes().Returns([]);

            GetServerOptions().Should().BeEmpty();
            ConfigureApplication(GetPlugin(), loggerFactory);

            logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning)
                .Which.Message.Should().Contain("hangfire:server:queues");
        }

        [Test]
        public void MissingNestedSectionsFallBackToTheDefaults()
        {
            _hangfireConfiguration.Server = null;
            _hangfireConfiguration.HealthCheck = null;
            _hangfireConfiguration.StatisticsMetrics = null;
            var plugin = GetPlugin();

            GetServerOptions().Should().ContainSingle();
            plugin.GetMinimumAvailableServers().Should().Be(1);
        }

        [Test]
        public void ShutdownTimeoutIsPassedToEveryServer()
        {
            _hangfireConfiguration.Server.ShutdownTimeoutInSeconds = 90;
            _hangfireConfiguration.QueueConfigurations = [new QueueConfiguration { Name = "bulk" }];

            var options = GetServerOptions();

            options.Should().HaveCount(2);
            options.Should().OnlyContain(x => x.ShutdownTimeout == TimeSpan.FromSeconds(90));
        }

        [Test]
        public void StatisticsMetricsReporterIsRegisteredUnlessSwitchedOff()
        {
            var enabled = new ServiceCollection();
            GetPlugin().ConfigureServices(enabled);
            enabled.Should().Contain(x => x.ImplementationType == typeof(HangfireStatisticsMetricsReporter));

            _hangfireConfiguration.StatisticsMetrics.Enabled = false;
            var disabled = new ServiceCollection();
            GetPlugin().ConfigureServices(disabled);
            disabled.Should().NotContain(x => x.ImplementationType == typeof(HangfireStatisticsMetricsReporter));
        }

        [Test]
        public void HealthCheckIsNotRegisteredWhenDisabled()
        {
            _hangfireConfiguration.HealthCheck.Enabled = false;
            var hcBuilder = Substitute.For<IHealthChecksBuilder>();

            GetPlugin().ConfigureHealthChecks(hcBuilder);

            hcBuilder.DidNotReceive().Add(Arg.Any<HealthCheckRegistration>());
        }

        [Test]
        public void HealthCheckExpectsOneServerOnlyWhenThisProcessStartsOne()
        {
            GetPlugin().GetMinimumAvailableServers().Should().Be(1);

            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = 0 };
            GetPlugin().GetMinimumAvailableServers().Should().BeNull();

            _hangfireConfiguration.Server.Queues = new Dictionary<string, int> { ["import"] = 2 };
            GetPlugin().GetMinimumAvailableServers().Should().Be(1);

            _hangfireConfiguration.Server.Enabled = false;
            GetPlugin().GetMinimumAvailableServers().Should().BeNull();
        }

        [Test]
        public void HealthCheckUsesTheConfiguredMinimumEvenWithoutAServer()
        {
            _hangfireConfiguration.Server.Enabled = false;
            _hangfireConfiguration.HealthCheck.MinimumAvailableServers = 3;

            GetPlugin().GetMinimumAvailableServers().Should().Be(3);
        }

        [Test, Parallelizable(ParallelScope.None)]
        public async Task HealthCheckIsHealthyWithoutAnyServerWhenTheServerIsDisabled()
        {
            _hangfireConfiguration.Server.Enabled = false;
            var plugin = GetPlugin();
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            sc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            plugin.ConfigureServices(sc);
            plugin.ConfigureHealthChecks(sc.AddHealthChecks());
            await using var sp = sc.BuildServiceProvider();
            // the host resolves the storage in ConfigureApplication, which is what publishes JobStorage.Current
            sp.GetRequiredService<JobStorage>().Should().NotBeNull();

            var report = await sp.GetRequiredService<HealthCheckService>().CheckHealthAsync();

            var entry = report.Entries.Should().ContainKey("hangfire").WhoseValue;
            entry.Status.Should().Be(HealthStatus.Healthy, $"{entry.Description} {entry.Exception}");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PrepareSchemaIfNecessaryIsPassedToTheStorageOptions(bool prepareSchema)
        {
            _hangfireConfiguration.PrepareSchemaIfNecessary = prepareSchema;
            _hangfireConfiguration.SchemaName = "jobs";

            var options = GetPlugin().CreateStorageOptions();

            options.PrepareSchemaIfNecessary.Should().Be(prepareSchema);
            options.SchemaName.Should().Be("jobs");
        }

        [Test]
        public void RecurringJobRegistrationCanBeSwitchedOff()
        {
            _hangfireConfiguration.RegisterRecurringJobs = false;
            var configured = false;
            var jobOptions = new JobOptions((_, _) => configured = true);

            ConfigureApplication(GetPlugin(), NullLoggerFactory.Instance, sc => sc.AddSingleton(jobOptions));

            configured.Should().BeFalse();
            _implementationResolver.DidNotReceiveWithAnyArgs().FindTypes();
        }

        [Test]
        public void RecurringJobRegistrationRunsTheJobOptionsByDefault()
        {
            _implementationResolver.FindTypes().Returns([]);
            var configured = false;
            var jobOptions = new JobOptions((_, _) => configured = true);

            ConfigureApplication(GetPlugin(), NullLoggerFactory.Instance, sc => sc.AddSingleton(jobOptions));

            configured.Should().BeTrue();
        }

        [Test]
        public void InvalidSslModeStopsThePostgresStorageAtConfigureServices()
        {
            _hangfireConfiguration.InMemory = false;
            _hangfireConfiguration.SslMode = "sometimes";

            var act = () => GetPlugin().ConfigureServices(new ServiceCollection());

            act.Should().Throw<InvalidOperationException>().WithMessage("*hangfire:sslMode*");
        }

        [Test, Parallelizable(ParallelScope.None)]
        public void DoesNotConfigureStorageIfConfiguratorsIncludeStorage()
        {
            JobStorage.Current = null;
            _configurator.IncludesStorage.Returns(true);
            _hangfireConfiguration.InMemory = false;
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            GetPlugin().ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            sp.GetRequiredService<IGlobalConfiguration>().Should().NotBeNull();
            var jobStorage = () => JobStorage.Current;
            var exception = jobStorage.Invoking(x => x())
                .Should()
                .Throw<InvalidOperationException>()
                .Which;
            exception.Message.Should().Contain("Current JobStorage instance has not been initialized yet");
        }
    }
}
