using AwesomeAssertions;
using Chronos.Abstractions;
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
    private IMessageScheduler _scheduler;
    private IDateTimeProvider _dateTimeProvider;
    private ISendEndpoint _sendEndpoint;
    private IMessageDeduplicateKeyProvider _deduplicateKeyProvider;

    [SetUp]
    public void Setup()
    {
        _sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(_sendEndpoint));
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _messageValidator = Substitute.For<IMessageValidator>();
        _scheduler = Substitute.For<IMessageScheduler>();
        _deduplicateKeyProvider = Substitute.For<IMessageDeduplicateKeyProvider>();
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler, _deduplicateKeyProvider);
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
        _deduplicateKeyProvider.TryGetKey(Arg.Any<object[]>()).Returns("abc");
        var message = new TestMessage(123);
        await _messageBus.SendAsync(message);
        await _messageBus.SendAsync(message);
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Any<Uri>());
        await sendEndpoint.Received(2).Send(Arg.Is<object>(t => t.GetType() == typeof(TestMessage) && ((TestMessage)t).Id == 123), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendAsyncSendsUntypedMessagesForMessageTypesWithConsumers()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(123);
        await _messageBus.SendAsync(typeof(TestMessage), message);
        await _sendEndpoint.Received(1).Send(Arg.Is<object>(o => o.Equals(message)),Arg.Any<IPipe<SendContext>>(),  Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThrowsExceptionWhenSchedulerIsNotPresent()
    {
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, null, _deduplicateKeyProvider);
        await _messageBus.Invoking(x => x.ScheduleAsync(new TestMessage(123), TimeSpan.FromSeconds(1)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SchedulesMessagesByTimeSpan()
    {
        var msg = new TestMessage(123);
        var ts = TimeSpan.FromSeconds(1);
        var date = new DateTime(2020, 1, 1);
        _dateTimeProvider.UtcNow.Returns(date);
        var expectedDate = date + ts;
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        await _messageBus.ScheduleAsync(msg, ts);
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), expectedDate, Arg.Is<object>(o => o.Equals(msg)), Arg.Any<IPipe<SendContext<object>>>(),   Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DoesNotSchedulesMessagesWhenThereIsNoConsumer()
    {
        var msg = new TestMessage(123);
        var ts = TimeSpan.FromSeconds(1);
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(false);
        await _messageBus.ScheduleAsync(msg, ts);
        _scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task SchedulesMessagesByDateTime()
    {
        var msg = new TestMessage(123);
        var date = new DateTime(2020, 1, 1);
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        await _messageBus.ScheduleAsync(msg, date);
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), date, Arg.Is<object>(o => o.Equals(msg)), Arg.Any<IPipe<SendContext<object>>>(),   Arg.Any<CancellationToken>());
    }

    private record TestMessage(int Id) : IMessage;
}
