using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Hosting.WebHost.Configurators;
using Dosaic.Hosting.WebHost.Logging;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Serilog.Core;

namespace Dosaic.Hosting.WebHost.Tests
{
    public class ServiceConfigurationTests
    {
        [Test]
        public void ConfigureTracingShouldWorkIfHostIsConfigured()
        {
            var configuration = CustomConfiguration.Create().Add("telemetry:endpoint", "http://localhost:1111").Build();
            var serviceCollection = TestingDefaults.ServiceCollection();

            var serviceConfigurator = new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration, serviceCollection, Substitute.For<IImplementationResolver>());
            serviceConfigurator.ConfigureTelemetry();

            serviceCollection.Should().Contain(x => x.ServiceType == typeof(ILogEventEnricher) && x.ImplementationType == typeof(OpentelemetryTraceEnricher));
            serviceCollection.Should().Contain(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType!.Name == "TelemetryHostedService");
            serviceCollection.Should().Contain(x => x.ServiceType == typeof(TracerProvider));
            serviceCollection.Should().Contain(x => x.ServiceType == typeof(LoggerProvider));
            Baggage.GetBaggage("AppName").Should().Be(Assembly.GetEntryAssembly()!.GetName().Name);
        }

        [Test]
        public void ConfigureTracingShouldNotWorkIfHostIsNotConfigured()
        {
            var configuration = CustomConfiguration.Create().Build();
            var serviceCollection = TestingDefaults.ServiceCollection();

            var serviceConfigurator = new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration, serviceCollection, Substitute.For<IImplementationResolver>());
            serviceConfigurator.ConfigureTelemetry();

            serviceCollection.Should().NotContain(x => x.ServiceType == typeof(ILogEventEnricher) && x.ImplementationType == typeof(OpentelemetryTraceEnricher));
            serviceCollection.Should().NotContain(x => x.ServiceType == typeof(TracerProvider));
            serviceCollection.Should().NotContain(x => x.ServiceType == typeof(LoggerProvider));
            Baggage.GetBaggage("AppName").Should().Be(Assembly.GetEntryAssembly()!.GetName().Name);
        }

        [Test]
        public void ConfigureTracingShouldUseConfiguredTelemetryName()
        {
            var configuration = CustomConfiguration.Create()
                .Add("telemetry:name", "custom-service-name")
                .Add("telemetry:endpoint", "http://localhost:1111")
                .Build();
            var serviceCollection = TestingDefaults.ServiceCollection();

            var serviceConfigurator = new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration, serviceCollection, Substitute.For<IImplementationResolver>());
            serviceConfigurator.ConfigureTelemetry();

            Baggage.GetBaggage("AppName").Should().Be("custom-service-name");
        }

        private static List<HealthCheckRegistration> ConfigureHealthChecks(IConfiguration configuration)
        {
            var serviceCollection = TestingDefaults.ServiceCollection();
            var implementationResolver = Substitute.For<IImplementationResolver>();
            implementationResolver.FindTypes().Returns([]);
            implementationResolver.FindAssemblies().Returns([]);

            new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration, serviceCollection, implementationResolver)
                .ConfigureHealthChecks();

            return serviceCollection.BuildServiceProvider()
                .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();
        }

        private static long GetMemoryThresholdInMegabytes(List<HealthCheckRegistration> registrations)
        {
            var healthCheck = registrations.Single(x => x.Name == "memory").Factory(TestingDefaults.ServiceProvider());
            var field = healthCheck.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return Convert.ToInt64(field.GetValue(healthCheck), CultureInfo.InvariantCulture);
        }

        [Test]
        public void MemoryHealthCheckDefaultsToTwoGigabytesOnLiveness()
        {
            var registrations = ConfigureHealthChecks(CustomConfiguration.Create().Build());

            registrations.Select(x => x.Name).Should().Equal("api", "disk_space", "memory");
            registrations.Should().OnlyContain(x => x.Tags.Contains("liveness"));
            GetMemoryThresholdInMegabytes(registrations).Should().Be(2048);
        }

        [Test]
        public void MemoryHealthCheckUsesTheConfiguredThreshold()
        {
            var configuration = CustomConfiguration.Create()
                .Add("host:healthChecks:memory:maximumMegabytes", "6144").Build();

            var registrations = ConfigureHealthChecks(configuration);

            registrations.Select(x => x.Name).Should().Equal("api", "disk_space", "memory");
            GetMemoryThresholdInMegabytes(registrations).Should().Be(6144);
        }

        [Test]
        public void MemoryHealthCheckThresholdCanBeSetThroughAnEnvironmentStyleKey()
        {
            var configuration = CustomConfiguration.Create()
                .Add("HOST:HEALTHCHECKS:MEMORY:MAXIMUMMEGABYTES", "512").Build();

            GetMemoryThresholdInMegabytes(ConfigureHealthChecks(configuration)).Should().Be(512);
        }

        [Test]
        public void MemoryHealthCheckIsNotRegisteredWhenTheThresholdIsZero()
        {
            var configuration = CustomConfiguration.Create()
                .Add("host:healthChecks:memory:maximumMegabytes", "0").Build();

            var registrations = ConfigureHealthChecks(configuration);

            registrations.Select(x => x.Name).Should().Equal("api", "disk_space");
        }

        [Test]
        public void MemoryHealthCheckUsesTheHostConfigurationItWasGiven()
        {
            var serviceCollection = TestingDefaults.ServiceCollection();
            var implementationResolver = Substitute.For<IImplementationResolver>();
            implementationResolver.FindTypes().Returns([]);
            implementationResolver.FindAssemblies().Returns([]);
            var hostConfiguration = new HostConfiguration();
            hostConfiguration.HealthChecks.Memory.MaximumMegabytes = 777;

            new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), CustomConfiguration.Empty, serviceCollection,
                implementationResolver, hostConfiguration).ConfigureHealthChecks();

            var registrations = serviceCollection.BuildServiceProvider()
                .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();
            GetMemoryThresholdInMegabytes(registrations).Should().Be(777);
        }

        [Test]
        public void NegativeMemoryHealthCheckThresholdThrowsAndNamesTheKey()
        {
            var configuration = CustomConfiguration.Create()
                .Add("host:healthChecks:memory:maximumMegabytes", "-1").Build();

            var act = () => ConfigureHealthChecks(configuration);

            act.Should().Throw<InvalidOperationException>().WithMessage("*host:healthChecks:memory:maximumMegabytes*");
        }
    }
}
