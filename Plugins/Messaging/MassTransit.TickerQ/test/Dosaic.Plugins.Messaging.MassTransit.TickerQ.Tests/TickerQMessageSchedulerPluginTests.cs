using AwesomeAssertions;
using Dosaic.Testing.NUnit;
using MassTransit;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ.Tests
{
    public class TickerQMessageSchedulerPluginTests
    {
        private TickerQMessageSchedulerPlugin _plugin;

        [SetUp]
        public void Setup()
        {
            var configuration = new TickerQMessageSchedulerConfiguration();
            _plugin = new TickerQMessageSchedulerPlugin(configuration);
        }

        [Test]
        public void ConfigureServicesRegistersMessageScheduler()
        {
            var sc = TestingDefaults.ServiceCollection();
            _plugin.ConfigureServices(sc);

            var descriptor = sc.FirstOrDefault(d => d.ServiceType == typeof(IMessageScheduler));
            descriptor.Should().NotBeNull();
            descriptor.ImplementationType.Should().Be(typeof(TickerQMessageScheduler));
        }

        [Test]
        public void ConfigureMassTransitDoesNotThrow()
        {
            var opts = Substitute.For<IBusRegistrationConfigurator>();
            var action = () => _plugin.ConfigureMassTransit(opts);
            action.Should().NotThrow();
        }
    }
}
