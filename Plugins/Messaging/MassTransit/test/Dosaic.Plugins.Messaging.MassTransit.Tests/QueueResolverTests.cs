using AwesomeAssertions;
using Dosaic.Plugins.Messaging.Abstractions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class QueueResolverTests
{
    private QueueResolver _resolver;

    [SetUp]
    public void Setup()
    {
        _resolver = new QueueResolver(new MessageBusConfiguration { Host = "localhost" }, []);
    }

    [Test]
    public void ShouldResolveListenAddressFromAttribute()
    {
        _resolver.ResolveListenAddress(typeof(AttrMessage)).Should().Be("queue:Attr_queue");
        _resolver.ResolveListenAddress(typeof(AttrMessage)).Should().Be("queue:Attr_queue");
    }

    [Test]
    public void ShouldResolveListenAddressFromTypeName()
    {
        _resolver.ResolveListenAddress(typeof(TypeMessage)).Should().Be("queue:TypeMessage");
        _resolver.ResolveListenAddress(typeof(TypeMessage)).Should().Be("queue:TypeMessage");
    }

    [Test]
    public void ShouldResolveListenAddressFromGenerics()
    {
        _resolver.ResolveListenAddress(typeof(GenericMessage<int, decimal>)).Should().Be("queue:GenericMessage-Int32-Decimal");
        _resolver.ResolveListenAddress(typeof(GenericMessage<int, decimal>)).Should().Be("queue:GenericMessage-Int32-Decimal");
    }

    [Test]
    public void ShouldResolveSendAddressAsExchangeForQuorumQueues()
    {
        var listenAddress = QueueResolver.BuildListenAddress(typeof(TypeMessage));
        var resolver = new QueueResolver(
            new MessageBusConfiguration { Host = "localhost", UseQuorumQueues = true },
            [(listenAddress, [typeof(TypeMessage)])]);
        resolver.ResolveSendAddress(typeof(TypeMessage)).Should().Be("exchange:TypeMessage");
    }

    [Test]
    public void ShouldResolveSendAddressAsQueueForClassicQueues()
    {
        _resolver.ResolveSendAddress(typeof(TypeMessage)).Should().Be("queue:TypeMessage");
    }

    [QueueName("Attr_queue")]
    private record AttrMessage : IMessage;
    private record TypeMessage : IMessage;
    private record GenericMessage<T1, T2> : IMessage;
}
