using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using MassTransit.Serialization;
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
        _deduplicateKeyProvider =
            new MessageDeduplicateKeyProvider(new MessageBusConfiguration
            {
                Deduplication = true,
                Host = "localhost"
            });
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
        _deduplicateKeyProvider = Substitute.For<IMessageDeduplicateKeyProvider>();
        _deduplicateKeyProvider.TryGetKey(Arg.Any<object[]>()).Returns(string.Empty);
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
        await _sendEndpoint.Received(1).Send(Arg.Is<object>(o => o.Equals(message)), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>());
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
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), expectedDate, Arg.Is<object>(o => o.Equals(msg)), Arg.Any<IPipe<SendContext<object>>>(), Arg.Any<CancellationToken>());
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
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), date, Arg.Is<object>(o => o.Equals(msg)), Arg.Any<IPipe<SendContext<object>>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SendsMessagesWithHeaders()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(123);
        var headers = new Dictionary<string, string> { { "key", "value" } };

        DictionarySendHeaders observedHeaders = null!;
        var durableWasSet = false;

        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext>)ci[1];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.SendAsync(typeof(TestMessage), message, headers);

        await _sendEndpoint.Received(1).Send(
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            Arg.Any<IPipe<SendContext>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeTrue();

        observedHeaders.Should().NotBeNull();
        observedHeaders.TryGetHeader("key", out var v).Should().BeTrue();
        v.Should().Be("value");

        // Deduplication header should also be present
        observedHeaders.TryGetHeader("x-deduplication-header", out var dedupe).Should().BeTrue();
        dedupe.Should().Be(_deduplicateKeyProvider.TryGetKey(message));
    }

    [Test]
    public async Task SendsMessagesWithDeDuplicateHeader()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);

        var message = new TestMessage(456);
        DictionarySendHeaders observedHeaders = null!;
        var durableWasSet = false;

        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext>)ci[1];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.SendAsync(typeof(TestMessage), message);

        await _sendEndpoint.Received(1).Send(
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            Arg.Any<IPipe<SendContext>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeTrue();
        observedHeaders.Should().NotBeNull();

        observedHeaders.TryGetHeader("x-deduplication-header", out var dedupe).Should().BeTrue();
        dedupe.Should().Be(_deduplicateKeyProvider.TryGetKey(message));
    }

    [Test]
    public async Task ScheduleMessagesWithHeaders()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(789);
        var date = new DateTime(2020, 1, 1);
        var headers = new Dictionary<string, string> { { "key", "value" } };
        var durableWasSet = false;

        DictionarySendHeaders observedHeaders = null!;

        _scheduler
            .When(s => s.ScheduleSend(
                Arg.Any<Uri>(),
                date,
                Arg.Any<object>(),
                Arg.Any<IPipe<SendContext<object>>>(),
                Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext<object>>)ci[3];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.ScheduleAsync(typeof(TestMessage), message, date, headers);

        await _scheduler.Received(1).ScheduleSend(
            Arg.Any<Uri>(),
            date,
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            Arg.Any<IPipe<SendContext<object>>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeTrue();
        observedHeaders.Should().NotBeNull();

        observedHeaders.TryGetHeader("key", out var v).Should().BeTrue();
        v.Should().Be("value");
    }

    [Test]
    public async Task ScheduleMessagesWithDeDuplicateHeader()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(999);
        var date = new DateTime(2020, 2, 2);
        var durableWasSet = false;

        DictionarySendHeaders observedHeaders = null!;

        _scheduler
            .When(s => s.ScheduleSend(
                Arg.Any<Uri>(),
                date,
                Arg.Any<object>(),
                Arg.Any<IPipe<SendContext<object>>>(),
                Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext<object>>)ci[3];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.ScheduleAsync(message, date);

        await _scheduler.Received(1).ScheduleSend(
            Arg.Any<Uri>(),
            date,
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            Arg.Any<IPipe<SendContext<object>>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeTrue();
        observedHeaders.Should().NotBeNull();
        observedHeaders.TryGetHeader("x-deduplication-header", out var dedupe).Should().BeTrue();
        dedupe.Should().Be(_deduplicateKeyProvider.TryGetKey(message));
    }

    private static DictionarySendHeaders ExecutePipeOnSendContext<TCtx>(IPipe<TCtx> pipe, out bool durableWasSet)
        where TCtx : class, SendContext
    {
        var localDurableWasSet = false;

        var fakeCtx = Substitute.For<TCtx>();
        var headers = new DictionarySendHeaders(new Dictionary<string, object>());
        fakeCtx.Headers.Returns(headers);
        fakeCtx.WhenForAnyArgs(x => x.Durable = false)
            .Do(ci => localDurableWasSet = (bool)ci.Args()[0]!);

        pipe.Send(fakeCtx).GetAwaiter().GetResult();

        durableWasSet = localDurableWasSet;
        return headers;
    }

    private record TestMessage(int Id) : IMessage;
}
