using System.Reflection;
using AwesomeAssertions;
using Dosaic.Hosting.WebHost;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    /// <summary>
    ///     Builds the real web host and switches roles with nothing but environment variables, the way a
    ///     Kubernetes deployment does. Environment variables and the Hangfire globals are process wide, so
    ///     these tests never run next to anything else.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    public class HangfireEnvironmentRolesTests
    {
        private readonly List<string> _variables = [];
        private List<JobFilter> _filtersBefore;

        [SetUp]
        public void Up()
        {
            _filtersBefore = GlobalJobFilters.Filters.ToList();
            Set("HANGFIRE_INMEMORY", "true");
        }

        [TearDown]
        public void Down()
        {
            foreach (var variable in _variables) Environment.SetEnvironmentVariable(variable, null);
            _variables.Clear();
            // the host registers filters that hold on to services of a host that is disposed by now
            foreach (var filter in GlobalJobFilters.Filters.ToList()
                         .Where(x => _filtersBefore.All(before => !ReferenceEquals(before.Instance, x.Instance))))
                GlobalJobFilters.Filters.Remove(filter.Instance);
        }

        private void Set(string variable, string value)
        {
            Environment.SetEnvironmentVariable(variable, value);
            _variables.Add(variable);
        }

        private static IHost BuildHost() =>
            PluginWebHostBuilder.Create([typeof(HangFirePlugin), typeof(HangfireConfiguration)]).Build();

        private static List<BackgroundJobServerOptions> GetServerOptions(IHost host)
        {
            var field = typeof(BackgroundJobServerHostedService)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return host.Services.GetServices<IHostedService>().OfType<BackgroundJobServerHostedService>()
                .Select(x => (BackgroundJobServerOptions)field.GetValue(x)).ToList();
        }

        private static async Task<HealthReportEntry> GetHangfireReadinessAsync(IHost host)
        {
            var report = await host.Services.GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(x => x.Tags.Contains("readiness"));
            return report.Entries["hangfire"];
        }

        [Test]
        public void WithoutRoleVariablesTheHostRunsTheSharedDefaultServer()
        {
            using var host = BuildHost();

            var server = GetServerOptions(host).Should().ContainSingle().Which;
            server.Queues.Should().Equal("default");
            server.ServerName.Should().BeNull();
            host.Services.GetServices<IHostedService>().OfType<HangfireStatisticsMetricsReporter>()
                .Should().ContainSingle();
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
                .Value.Registrations.Should().ContainSingle(x => x.Name == "hangfire")
                .Which.Tags.Should().Contain("readiness");
        }

        [Test]
        public async Task ServerEnabledFalseStartsAHostWithoutServersThatEnqueuesAndIsReady()
        {
            Set("HANGFIRE_SERVER_ENABLED", "false");
            using var host = BuildHost();

            GetServerOptions(host).Should().BeEmpty();
            host.Services.GetRequiredService<JobStorage>().GetMonitoringApi().Servers().Should().BeEmpty();
            host.Services.GetRequiredService<IJobManager>().Enqueue<Integration.NoopJob>("envrole")
                .Should().NotBeNullOrEmpty();
            var readiness = await GetHangfireReadinessAsync(host);
            readiness.Status.Should().Be(HealthStatus.Healthy, readiness.Description);
        }

        [Test]
        public void ServerQueuesVariableServesOnlyThatQueueWithItsWorkerCount()
        {
            Set("HANGFIRE_SERVER_QUEUES_IMPORT", "40");
            using var host = BuildHost();

            var server = GetServerOptions(host).Should().ContainSingle().Which;
            server.Queues.Should().Equal("import");
            server.WorkerCount.Should().Be(40);
            server.ServerName.Should().EndWith(":import");
        }

        [Test]
        public void EveryRoleSwitchCanBeSetThroughTheEnvironment()
        {
            Set("HANGFIRE_SERVER_QUEUES_IMPORT", "3");
            Set("HANGFIRE_SERVER_QUEUES_MAIL", "0");
            Set("HANGFIRE_SERVER_SHUTDOWNTIMEOUTINSECONDS", "90");
            Set("HANGFIRE_HEALTHCHECK_ENABLED", "false");
            Set("HANGFIRE_STATISTICSMETRICS_ENABLED", "false");
            using var host = BuildHost();

            var server = GetServerOptions(host).Should().ContainSingle().Which;
            server.Queues.Should().Equal("import");
            server.ShutdownTimeout.Should().Be(TimeSpan.FromSeconds(90));
            host.Services.GetServices<IHostedService>().OfType<HangfireStatisticsMetricsReporter>().Should().BeEmpty();
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
                .Value.Registrations.Should().NotContain(x => x.Name == "hangfire");
        }

        [Test]
        public void InvalidQueueNameFromTheEnvironmentStopsTheHost()
        {
            Set("HANGFIRE_SERVER_QUEUES_NOT.VALID", "1");

            var act = () => BuildHost();

            act.Should().Throw<Exception>().Which.ToString().Should().Contain("hangfire:server:queues:NOT.VALID");
        }
    }
}
