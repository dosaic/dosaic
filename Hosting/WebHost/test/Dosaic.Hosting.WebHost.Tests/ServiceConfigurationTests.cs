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
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
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

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static List<object> GetMetricExporters(IConfiguration configuration,
            Dictionary<string, string> otelEnvironment = null, IServiceCollection serviceCollection = null)
        {
            serviceCollection ??= TestingDefaults.ServiceCollection();
            if (otelEnvironment is not null)
                serviceCollection.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(otelEnvironment).Build());
            new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration, serviceCollection,
                Substitute.For<IImplementationResolver>()).ConfigureTelemetry();
            using var serviceProvider = serviceCollection.BuildServiceProvider();
            // the SDK keeps its metric readers and their exporters internal
            var reader = GetMember(serviceProvider.GetRequiredService<MeterProvider>(), "Reader");
            var readers = new List<object>();
            var node = GetMember(reader, "Head");
            if (node is null) readers.Add(reader);
            while (node is not null)
            {
                readers.Add(GetMember(node, "Value"));
                node = GetMember(node, "Next");
            }

            return readers.Select(x => GetMember(x, "Exporter")).ToList();
        }

        private static object GetMember(object instance, string name)
        {
            for (var type = instance.GetType(); type is not null; type = type.BaseType)
            {
                if (type.GetField(name, AnyInstance) is { } field) return field.GetValue(instance);
                if (type.GetProperty(name, AnyInstance) is { } property) return property.GetValue(instance);
            }

            return null;
        }

        private static Uri FindEndpoint(object instance, int depth = 4)
        {
            if (instance is null || depth == 0) return null;
            // the http export client of some signals is created on first use
            if (instance.GetType().IsGenericType && instance.GetType().GetGenericTypeDefinition() == typeof(Lazy<>))
                return FindEndpoint(instance.GetType().GetProperty("Value")!.GetValue(instance), depth);
            for (var type = instance.GetType(); type is not null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(AnyInstance | BindingFlags.DeclaredOnly))
                {
                    var value = field.GetValue(instance);
                    if (value is Uri uri) return uri;
                    if (value is null || field.FieldType.IsValueType || value is string) continue;
                    if (FindEndpoint(value, depth - 1) is { } found) return found;
                }
            }

            return null;
        }

        [TestCase(null)]
        [TestCase("")]
        public void MetricsAreOnlyExposedForPrometheusWithoutAnEndpoint(string endpoint)
        {
            var configuration = endpoint is null
                ? CustomConfiguration.Create().Build()
                : CustomConfiguration.Create().Add("telemetry:endpoint", endpoint).Build();

            var exporters = GetMetricExporters(configuration);

            exporters.Select(x => x.GetType().Name).Should().Equal("PrometheusExporter");
        }

        [Test]
        public void OtelExporterEndpointVariableAloneDoesNotTurnOnAnyOtlpExport()
        {
            var serviceCollection = TestingDefaults.ServiceCollection();
            var exporters = GetMetricExporters(CustomConfiguration.Create().Build(),
                new Dictionary<string, string> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317" },
                serviceCollection);

            exporters.Select(x => x.GetType().Name).Should().Equal("PrometheusExporter");
            serviceCollection.Should().NotContain(x => x.ServiceType == typeof(TracerProvider));
            serviceCollection.Should().NotContain(x => x.ServiceType == typeof(LoggerProvider));
        }

        [Test]
        public void MetricsAreExportedViaOtlpAndPrometheusWithAnEndpoint()
        {
            var configuration = CustomConfiguration.Create().Add("telemetry:endpoint", "http://localhost:1111").Build();

            var exporters = GetMetricExporters(configuration);

            exporters.Select(x => x.GetType().Name).Should().Equal("OtlpMetricExporter", "PrometheusExporter");
        }

        [Test]
        public void ConfiguredEndpointWinsOverTheOtelExporterEndpointVariable()
        {
            var configuration = CustomConfiguration.Create().Add("telemetry:endpoint", "http://localhost:1111").Build();

            var exporters = GetMetricExporters(configuration,
                new Dictionary<string, string> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317" });

            FindEndpoint(exporters[0])!.Authority.Should().Be("localhost:1111");
        }

        private static Dictionary<string, Uri> GetOtlpEndpoints(string endpoint, string protocol)
        {
            var configuration = CustomConfiguration.Create()
                .Add("telemetry:endpoint", endpoint)
                .Add("telemetry:protocol", protocol)
                .Build();
            var serviceCollection = TestingDefaults.ServiceCollection();
            var metricExporter = GetMetricExporters(configuration, serviceCollection: serviceCollection)[0];
            using var serviceProvider = serviceCollection.BuildServiceProvider();
            var traceExporter = GetMember(GetMember(serviceProvider.GetRequiredService<TracerProvider>(), "Processor"),
                "exporter");
            var logExporter = GetMember(GetMember(serviceProvider.GetRequiredService<LoggerProvider>(), "Processor"),
                "exporter");
            return new Dictionary<string, Uri>
            {
                ["traces"] = FindEndpoint(traceExporter),
                ["metrics"] = FindEndpoint(metricExporter),
                ["logs"] = FindEndpoint(logExporter)
            };
        }

        [TestCase("http/protobuf")]
        [TestCase("HttpProtobuf")]
        [TestCase("HTTP/PROTOBUF")]
        public void HttpProtobufPostsEverySignalToItsOwnPath(string protocol)
        {
            var endpoints = GetOtlpEndpoints("http://collector:4318", protocol);

            endpoints["traces"].Should().Be(new Uri("http://collector:4318/v1/traces"));
            endpoints["metrics"].Should().Be(new Uri("http://collector:4318/v1/metrics"));
            endpoints["logs"].Should().Be(new Uri("http://collector:4318/v1/logs"));
        }

        [Test]
        public void HttpProtobufKeepsTheBasePathOfTheEndpoint()
        {
            var endpoints = GetOtlpEndpoints("https://gateway.example.com/otlp/", "http/protobuf");

            endpoints["traces"].Should().Be(new Uri("https://gateway.example.com/otlp/v1/traces"));
            endpoints["metrics"].Should().Be(new Uri("https://gateway.example.com/otlp/v1/metrics"));
            endpoints["logs"].Should().Be(new Uri("https://gateway.example.com/otlp/v1/logs"));
        }

        [TestCase("grpc")]
        [TestCase("Grpc")]
        [TestCase("")]
        public void GrpcSendsEverySignalToTheConfiguredEndpoint(string protocol)
        {
            var endpoints = GetOtlpEndpoints("http://collector:4317", protocol);

            endpoints.Values.Should().AllSatisfy(x =>
            {
                x.Authority.Should().Be("collector:4317");
                x.AbsolutePath.Should().NotContain("v1/");
            });
        }

        [TestCase("http://collector:4318", "http://collector:4318/v1/traces")]
        [TestCase("http://collector:4318/", "http://collector:4318/v1/traces")]
        [TestCase("http://collector:4318/otlp", "http://collector:4318/otlp/v1/traces")]
        [TestCase("http://collector:4318/otlp/?tenant=a", "http://collector:4318/otlp/v1/traces?tenant=a")]
        public void HttpProtobufEndpointGetsTheSignalPath(string endpoint, string expected)
        {
            ServiceConfigurator.GetOtlpEndpoint(new Uri(endpoint), OtlpExportProtocol.HttpProtobuf,
                    ServiceConfigurator.OtlpSignalPaths.Traces)
                .Should().Be(new Uri(expected));
        }

        [Test]
        public void GrpcEndpointIsNotChanged()
        {
            var endpoint = new Uri("http://collector:4317/");

            ServiceConfigurator.GetOtlpEndpoint(endpoint, OtlpExportProtocol.Grpc,
                ServiceConfigurator.OtlpSignalPaths.Traces).Should().BeSameAs(endpoint);
        }

        [TestCase(null, OtlpExportProtocol.Grpc)]
        [TestCase(" ", OtlpExportProtocol.Grpc)]
        [TestCase("grpc", OtlpExportProtocol.Grpc)]
        [TestCase("GRPC", OtlpExportProtocol.Grpc)]
        [TestCase("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
        [TestCase("HttpProtobuf", OtlpExportProtocol.HttpProtobuf)]
        [TestCase("http-protobuf", OtlpExportProtocol.HttpProtobuf)]
        public void OtlpProtocolAcceptsTheSpecificationAndTheEnumNames(string value, OtlpExportProtocol expected)
        {
            ServiceConfigurator.ParseOtlpProtocol(value).Should().Be(expected);
        }

        [Test]
        public void UnknownOtlpProtocolStopsTheStartAndNamesTheKey()
        {
            var configuration = CustomConfiguration.Create()
                .Add("telemetry:endpoint", "http://collector:4318")
                .Add("telemetry:protocol", "http/json")
                .Build();

            var configure = () => new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), configuration,
                TestingDefaults.ServiceCollection(), Substitute.For<IImplementationResolver>()).ConfigureTelemetry();

            configure.Should().Throw<InvalidOperationException>()
                .WithMessage("telemetry:protocol 'http/json' is not supported. Use 'grpc' or 'http/protobuf'.");
        }

        [Test]
        public void UnknownOtlpProtocolIsIgnoredWithoutAnEndpoint()
        {
            var configuration = CustomConfiguration.Create().Add("telemetry:protocol", "http/json").Build();

            GetMetricExporters(configuration).Select(x => x.GetType().Name).Should().Equal("PrometheusExporter");
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
        public void AHostConfigurationTheResolverBuiltItselfIsNotRegisteredASecondTime()
        {
            var serviceCollection = TestingDefaults.ServiceCollection();
            var implementationResolver = Substitute.For<IImplementationResolver>();
            implementationResolver.FindTypes().Returns([typeof(HostConfiguration)]);
            implementationResolver.FindAssemblies().Returns([]);
            implementationResolver.ResolveInstance(typeof(HostConfiguration)).Returns(new HostConfiguration());
            var hostConfiguration = new HostConfiguration();

            new ServiceConfigurator(new FakeLogger<ServiceConfigurator>(), CustomConfiguration.Empty, serviceCollection,
                implementationResolver, hostConfiguration).Configure();

            serviceCollection.Where(x => x.ServiceType == typeof(HostConfiguration)).Should().ContainSingle()
                .Which.ImplementationInstance.Should().BeSameAs(hostConfiguration);
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
