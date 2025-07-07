using AwesomeAssertions;
using Dosaic.Plugins.Messaging.Abstractions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class QueueResolverTests
{
    [Test]
    public void ShouldResolveQueueNameFromAttribute()
    {
        QueueResolver.Resolve<AttrMessage>().Should().Be("queue:Attr_queue");
        QueueResolver.Resolve<AttrMessage>().Should().Be("queue:Attr_queue");
    }

    [Test]
    public void ShouldResolveQueueNameFromTypeName()
    {
        QueueResolver.Resolve<TypeMessage>().Should().Be("queue:TypeMessage");
        QueueResolver.Resolve<TypeMessage>().Should().Be("queue:TypeMessage");
    }

    [Test]
    public void ShouldResolveQueueNameFromGenerics()
    {
        QueueResolver.Resolve<GenericMessage<int, decimal>>().Should().Be("queue:GenericMessage-Int32-Decimal");
        QueueResolver.Resolve<GenericMessage<int, decimal>>().Should().Be("queue:GenericMessage-Int32-Decimal");
    }

    [QueueName("Attr_queue")]
    private record AttrMessage : IMessage;
    private record TypeMessage : IMessage;
    private record GenericMessage<T1, T2> : IMessage;
}
