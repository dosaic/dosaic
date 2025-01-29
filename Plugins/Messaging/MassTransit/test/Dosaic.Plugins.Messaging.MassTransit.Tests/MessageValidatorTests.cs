using Dosaic.Plugins.Messaging.Abstractions;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class MessageValidatorTests
{
    [Test]
    public void ChecksIfMessageTypeIsInConsumerTypeArray()
    {
        var validator = new MessageValidator([typeof(Msg1)]);
        validator.HasConsumers<Msg1>().Should().BeTrue();
        validator.HasConsumers<Msg2>().Should().BeFalse();
    }

    private record Msg1 : IMessage;
    private record Msg2 : IMessage;
}
