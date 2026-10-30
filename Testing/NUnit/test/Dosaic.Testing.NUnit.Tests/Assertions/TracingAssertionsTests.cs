using AwesomeAssertions;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Dosaic.Testing.NUnit.Tests.Assertions
{
    public class TracingAssertionsTests
    {
        private ServiceProvider _serviceProvider;

        [SetUp]
        public void Setup()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton(Sdk.CreateTracerProviderBuilder()
                .AddSource("Dosaic.Tests.Exact", "Dosaic.Tests.Wildcard.*")
                .Build());
            _serviceProvider = sc.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown() => _serviceProvider.Dispose();

        [Test]
        public void RegisterSourcesSucceedsForRegisteredSources()
        {
            _serviceProvider.Should().RegisterSources("Dosaic.Tests.Exact");
            _serviceProvider.Should().RegisterSources(["Dosaic.Tests.Exact", "Dosaic.Tests.Wildcard.Child"]);
        }

        [Test]
        public void RegisterSourcesFailsForUnregisteredSources()
        {
            var act = () => _serviceProvider.Should().RegisterSources(["Dosaic.Tests.Exact", "Dosaic.Tests.Unknown"]);

            act.Should().Throw<AssertionException>().WithMessage("*Missing*Dosaic.Tests.Unknown*");
        }
    }
}
