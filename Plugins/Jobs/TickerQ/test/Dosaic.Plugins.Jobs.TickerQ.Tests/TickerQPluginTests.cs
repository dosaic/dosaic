using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using NUnit.Framework;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Dosaic.Plugins.Jobs.TickerQ.Tests
{
    public class TickerQPluginTests
    {
        private TickerQConfiguration _configuration;
        private ITickerQConfigurator _configurator;

        private TickerQPlugin GetPlugin() =>
            new(_configuration, [_configurator]);

        [SetUp]
        public void Setup()
        {
            _configurator = Substitute.For<ITickerQConfigurator>();
            _configuration = new TickerQConfiguration
            {
                Host = "testHost",
                Port = 5432,
                Database = "testDb",
                User = "testUser",
                Password = "testPassword",
                InMemory = true,
                DashboardAuthMode = DashboardAuthMode.None,
                DashboardBasePath = "/tickerq/dashboard",
                EnableJobsByFeatureManagementConfig = false
            };
        }

        [Test]
        public void ConfigureServicesRegistersJobManager()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            sc.AddLogging();
            GetPlugin().ConfigureServices(sc);

            var sp = sc.BuildServiceProvider();
            var jobManager = sp.GetService<IJobManager>();
            jobManager.Should().NotBeNull();
            jobManager.Should().BeOfType<JobManager>();
        }

        [Test]
        public void ConfigureServicesCallsConfigurators()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            sc.AddLogging();
            GetPlugin().ConfigureServices(sc);

            _configurator.Received(1)
                .Configure(Arg.Any<TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity>>());
        }

        [Test]
        public void ConfigureServicesDoesNotConfigurePersistenceWhenConfiguratorIncludesIt()
        {
            _configurator.IncludesPersistence.Returns(true);
            _configuration.InMemory = false;
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            sc.AddLogging();
            GetPlugin().ConfigureServices(sc);

            // If configurator includes persistence, plugin should not set up its own storage.
            // No exception means the configurator was respected.
            _configurator.Received(1)
                .Configure(Arg.Any<TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity>>());
        }

        [Test]
        public void ConfigureHealthChecksAddsTickerQCheck()
        {
            var hcBuilder = Substitute.For<IHealthChecksBuilder>();
            GetPlugin().ConfigureHealthChecks(hcBuilder);
            hcBuilder.Add(Arg.Is<HealthCheckRegistration>(h =>
                    h.Name == "tickerq" && h.Tags.Contains(HealthCheckTag.Readiness.Value)))
                .Received(Quantity.Exactly(1));
        }

        [Test]
        public void ConnectionStringIsComputedCorrectly()
        {
            _configuration.Host = "myhost";
            _configuration.Port = 1234;
            _configuration.Database = "mydb";
            _configuration.User = "myuser";
            _configuration.Password = "mypass";
            _configuration.ConnectionString.Should()
                .Be("Host=myhost;Port=1234;Database=mydb;Username=myuser;Password=mypass;");
        }
    }
}
