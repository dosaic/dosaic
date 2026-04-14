using AwesomeAssertions;
using MassTransit;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class MessageBusConfiguratorTests
{
    private class TestConfigurator : IMessageBusConfigurator;

    private class DelegatingConfigurator : IMessageBusConfigurator
    {
        public bool ThreeParamCalled { get; private set; }
        public Uri ReceivedQueue { get; private set; }

        void IMessageBusConfigurator.ConfigureReceiveEndpoint(IBusRegistrationContext context, Uri queue,
            IRabbitMqReceiveEndpointConfigurator configurator)
        {
            ThreeParamCalled = true;
            ReceivedQueue = queue;
        }
    }

    [Test]
    public void ConfigureMassTransitShouldBeNoOp()
    {
        IMessageBusConfigurator configurator = new TestConfigurator();
        var opts = Substitute.For<IBusRegistrationConfigurator>();
        configurator.ConfigureMassTransit(opts);
    }

    [Test]
    public void ConfigureRabbitMqShouldBeNoOp()
    {
        IMessageBusConfigurator configurator = new TestConfigurator();
        var context = Substitute.For<IBusRegistrationContext>();
        var config = Substitute.For<IRabbitMqBusFactoryConfigurator>();
        configurator.ConfigureRabbitMq(context, config);
    }

    [Test]
    public void ConfigureReceiveEndpointShouldBeNoOp()
    {
        IMessageBusConfigurator configurator = new TestConfigurator();
        var context = Substitute.For<IBusRegistrationContext>();
        var queue = new Uri("queue:test");
        var endpointConfigurator = Substitute.For<IRabbitMqReceiveEndpointConfigurator>();
        configurator.ConfigureReceiveEndpoint(context, queue, endpointConfigurator);
    }

    [Test]
    public void FourParamConfigureReceiveEndpointShouldDelegateToThreeParam()
    {
        var configurator = new DelegatingConfigurator();
        var context = Substitute.For<IBusRegistrationContext>();
        var queue = new Uri("queue:test");
        var endpointConfigurator = Substitute.For<IRabbitMqReceiveEndpointConfigurator>();
        ((IMessageBusConfigurator)configurator).ConfigureReceiveEndpoint(context, queue, [typeof(string)],
            endpointConfigurator);
        configurator.ThreeParamCalled.Should().BeTrue();
        configurator.ReceivedQueue.Should().Be(queue);
    }
}
