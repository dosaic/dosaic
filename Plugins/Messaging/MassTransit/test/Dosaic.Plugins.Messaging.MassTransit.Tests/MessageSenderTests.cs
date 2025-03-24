using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class MessageSenderTests
{
    private IMessageValidator _messageValidator;
    private ISendEndpointProvider _sendEndpointProvider;
    private IMessageBus _messageBus;

    [SetUp]
    public void Setup()
    {
        _messageValidator = Substitute.For<IMessageValidator>();
        _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        _messageBus = new MessageSender(_sendEndpointProvider, _messageValidator);
    }

    [Test]
    public async Task SendAsyncDoesNotSendAMessageWhenThereAreNoConsumers()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(false);
        await _messageBus.SendAsync(new TestMessage(123));
        await _sendEndpointProvider.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
    }

    [Test]
    public async Task SendAsyncDoesNotSendAUntypedMessageWhenThereAreNoConsumers()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(false);
        await _messageBus.SendAsync(typeof(TestMessage), new TestMessage(123));
        await _sendEndpointProvider.DidNotReceive().GetSendEndpoint(Arg.Any<Uri>());
    }

    [Test]
    public async Task SendAsyncSendsMessagesForMessageTypesWithConsumers()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(sendEndpoint));
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(123);
        await _messageBus.SendAsync(message);
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Any<Uri>());
        await sendEndpoint.Received(1).Send(Arg.Is<object>(t => t.GetType() == typeof(TestMessage) && ((TestMessage)t).Id == 123), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendAsyncSendsUntypedMessagesForMessageTypesWithConsumers()
    {
        var sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(sendEndpoint));
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(123);
        await _messageBus.SendAsync(typeof(TestMessage), message);
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Any<Uri>());
        await sendEndpoint.Received(1).Send(Arg.Is<object>(t => t.GetType() == typeof(TestMessage) && ((TestMessage)t).Id == 123), Arg.Any<CancellationToken>());
    }

    private record TestMessage(int Id) : IMessage;
}
