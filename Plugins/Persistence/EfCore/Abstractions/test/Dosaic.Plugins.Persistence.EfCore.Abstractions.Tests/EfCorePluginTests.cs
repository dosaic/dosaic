using System.Data;
using System.Diagnostics;
using Chronos;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Monitoring;
using Dosaic.Testing.NUnit.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NUnit.Framework;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    public class EfCorePluginTests
    {
        private IImplementationResolver _implementationResolver;
        private EfCorePlugin _plugin;

        [SetUp]
        public void Up()
        {
            _implementationResolver = Substitute.For<IImplementationResolver>();
            _plugin = new EfCorePlugin(_implementationResolver);
        }

        [Test]
        public void ConfigureServicesWorks()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            _plugin.ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();

        }

        [Test]
        public void ConfigureApplicationWorks()
        {
            var appBuilder = Substitute.For<IApplicationBuilder>();
            _plugin.ConfigureApplication(appBuilder);

            var subscriptions = DiagnosticListener.AllListeners.GetInaccessibleValue<object>("_subscriptions");
            subscriptions.Should().NotBeNull();
            var subscriber = subscriptions.GetInaccessibleValue<object>("Subscriber");
            subscriber.Should().NotBeNull();
            subscriber.Should().BeOfType<DiagnosticObserver>();
        }

        [Test]
        public void ConfiguresHealthChecks()
        {
            _implementationResolver.FindAssemblies().Returns([typeof(EfCorePluginTests).Assembly]);
            var healthCheckBuilder = Substitute.For<IHealthChecksBuilder>();
            _plugin.ConfigureHealthChecks(healthCheckBuilder);
            healthCheckBuilder.Received(1).Add(Arg.Any<HealthCheckRegistration>());
            healthCheckBuilder.Received(1).Add(Arg.Is<HealthCheckRegistration>(h => h.Name == nameof(TestContext)));
        }

        [Test]
        public void EnrichEfCoreWithActivitySetsOptions()
        {
            var opts = new EntityFrameworkInstrumentationOptions();
            EfCorePlugin.EnrichEfCoreWithActivity(opts);
            opts.EnrichWithIDbCommand.Should().NotBeNull();
            using var activity = new Activity("unit-test");
            var command = Substitute.For<IDbCommand>();
            opts.EnrichWithIDbCommand!(activity, command);

            var dbNameTag = activity.Tags.Single(x => x.Key == "db.name");
            dbNameTag.Should().NotBeNull();
            dbNameTag.Value.Should().Be(command.CommandType + " main");
            activity.DisplayName.Should().Be(command.CommandType + " main");
        }
    }
}
