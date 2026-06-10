using System.Diagnostics;
using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Testing.NUnit.Extensions;
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
    private IQueueResolver _queueResolver;
    private MessageBusConfiguration _configuration;
    private static readonly DateTime _now = DateTime.UtcNow;

    [SetUp]
    public void Setup()
    {
        _sendEndpoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
        _sendEndpointProvider.GetSendEndpoint(Arg.Any<Uri>()).Returns(Task.FromResult(_sendEndpoint));
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.UtcNow.Returns(_now);
        _messageValidator = Substitute.For<IMessageValidator>();
        _scheduler = Substitute.For<IMessageScheduler>();
        _deduplicateKeyProvider =
            new MessageDeduplicateKeyProvider(new MessageBusConfiguration
            {
                Deduplication = true,
                Host = "localhost"
            });
        _queueResolver = new QueueResolver(new MessageBusConfiguration { Host = "localhost" }, []);
        _configuration = new MessageBusConfiguration();
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler, _deduplicateKeyProvider, _queueResolver, _configuration);
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
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, null, _deduplicateKeyProvider, _queueResolver, _configuration);
        await _messageBus.Invoking(x => x.ScheduleAsync(new TestMessage(123), TimeSpan.FromSeconds(1)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SchedulesMessagesByTimeSpan()
    {
        var msg = new TestMessage(123);
        var ts = TimeSpan.FromDays(1); ;
        var expectedDate = _now + ts;
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        await _messageBus.ScheduleAsync(msg, ts);
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), ArgExt.Is<DateTime>(d => d.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(3))), Arg.Is<object>(o => o.Equals(msg)), typeof(TestMessage), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>());
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
        var date = _now.AddDays(2);
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        await _messageBus.ScheduleAsync(msg, date);
        await _scheduler.Received(1).ScheduleSend(Arg.Any<Uri>(), ArgExt.Is<DateTime>(d => d.Should().BeCloseTo(date, TimeSpan.FromSeconds(3))), Arg.Is<object>(o => o.Equals(msg)), typeof(TestMessage), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>());

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

        durableWasSet.Should().BeFalse();

        observedHeaders.Should().NotBeNull();
        observedHeaders.TryGetHeader("key", out var v).Should().BeTrue();
        v.Should().Be("value");

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

        durableWasSet.Should().BeFalse();
        observedHeaders.Should().NotBeNull();

        observedHeaders.TryGetHeader("x-deduplication-header", out var dedupe).Should().BeTrue();
        dedupe.Should().Be(_deduplicateKeyProvider.TryGetKey(message));
    }

    [Test]
    public async Task ScheduleMessagesWithHeaders()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(789);
        var duration = new TimeSpan(1, 1, 1);

        var headers = new Dictionary<string, string> { { "key", "value" } };
        var durableWasSet = false;

        DictionarySendHeaders observedHeaders = null!;

        _scheduler
            .When(s => s.ScheduleSend(
                Arg.Any<Uri>(),
                Arg.Any<DateTime>(),
                Arg.Any<object>(),
                Arg.Any<Type>(),
                Arg.Any<IPipe<SendContext>>(),
                Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext>)ci[4];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.ScheduleAsync(typeof(TestMessage), message, duration, headers);

        await _scheduler.Received(1).ScheduleSend(
            Arg.Any<Uri>(),
            Arg.Any<DateTime>(),
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            typeof(TestMessage),
            Arg.Any<IPipe<SendContext>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeFalse();
        observedHeaders.Should().NotBeNull();

        observedHeaders.TryGetHeader("key", out var v).Should().BeTrue();
        v.Should().Be("value");
    }

    [Test]
    public async Task ScheduleMessagesWithDeDuplicateHeader()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var message = new TestMessage(999);
        var date = new DateTime(2020, 2, 2).TimeOfDay;
        var durableWasSet = false;

        DictionarySendHeaders observedHeaders = null!;

        _scheduler
            .When(s => s.ScheduleSend(
                Arg.Any<Uri>(),
                Arg.Any<DateTime>(),
                Arg.Any<object>(),
                Arg.Any<Type>(),
                Arg.Any<IPipe<SendContext>>(),
                Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var pipe = (IPipe<SendContext<object>>)ci[4];
                observedHeaders = ExecutePipeOnSendContext(pipe, out durableWasSet);
            });

        await _messageBus.ScheduleAsync(message, date);

        await _scheduler.Received(1).ScheduleSend(
            Arg.Any<Uri>(),
            Arg.Any<DateTime>(),
            Arg.Is<object>(o => ReferenceEquals(o, message)),
            typeof(TestMessage),
            Arg.Any<IPipe<SendContext>>(),
            Arg.Any<CancellationToken>());

        durableWasSet.Should().BeFalse();
        observedHeaders.Should().NotBeNull();
        observedHeaders.TryGetHeader("x-deduplication-header", out var dedupe).Should().BeTrue();
        dedupe.Should().Be(_deduplicateKeyProvider.TryGetKey(message));
    }

    private static ActivityListener StartListener(List<Activity> started = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => { if (a.OperationName.EndsWith(" send")) started?.Add(a); }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public async Task SendAsyncCreatesSendSpanUnderCallerAndLinksConsumerViaHeader()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        using var source = new ActivitySource("test-sender-send");
        using var ambient = source.StartActivity("job");
        ambient.Should().NotBeNull();

        DictionarySendHeaders observedHeaders = null!;
        Activity duringSend = null;
        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                duringSend = Activity.Current;
                observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[1], out _);
            });

        await _messageBus.SendAsync(typeof(TestMessage), new TestMessage(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG TestMessage send");
        sendSpan.Kind.Should().Be(ActivityKind.Producer);
        sendSpan.Parent.Should().Be(ambient, "the send span lives in the caller's trace");
        duringSend.Should().BeNull("the ambient activity is suppressed during the transport call");
        Activity.Current.Should().Be(ambient, "the ambient activity is restored afterwards");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out var link).Should().BeTrue();
        link.Should().Be(sendSpan.Id);
    }

    [Test]
    public async Task SendAsyncCreatesRootSendSpanWhenNoAmbientActivity()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        Activity.Current.Should().BeNull();

        DictionarySendHeaders observedHeaders = null!;
        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci => observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[1], out _));

        await _messageBus.SendAsync(typeof(TestMessage), new TestMessage(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG TestMessage send");
        sendSpan.Parent.Should().BeNull("with no caller the send span is its own root");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out var link).Should().BeTrue();
        link.Should().Be(sendSpan.Id);
    }

    [Test]
    public async Task ScheduleAsyncCreatesSendSpanUnderCallerAndLinksConsumerViaHeader()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        using var source = new ActivitySource("test-sender-schedule");
        using var ambient = source.StartActivity("job");
        ambient.Should().NotBeNull();

        DictionarySendHeaders observedHeaders = null!;
        Activity duringSend = null;
        _scheduler
            .When(s => s.ScheduleSend(Arg.Any<Uri>(), Arg.Any<DateTime>(), Arg.Any<object>(), Arg.Any<Type>(),
                Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                duringSend = Activity.Current;
                observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[4], out _);
            });

        await _messageBus.ScheduleAsync(new TestMessage(1), TimeSpan.FromSeconds(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG TestMessage send");
        sendSpan.Parent.Should().Be(ambient);
        duringSend.Should().BeNull();
        Activity.Current.Should().Be(ambient);
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out var link).Should().BeTrue();
        link.Should().Be(sendSpan.Id);
    }

    [Test]
    public async Task SendAsyncUsesParentHeaderWhenTraceLinksDisabled()
    {
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler,
            _deduplicateKeyProvider, _queueResolver, new MessageBusConfiguration { UseTraceLinks = false });
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        using var source = new ActivitySource("test-sender-disabled");
        using var ambient = source.StartActivity("job");

        DictionarySendHeaders observedHeaders = null!;
        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci => observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[1], out _));

        await _messageBus.SendAsync(typeof(TestMessage), new TestMessage(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG TestMessage send");
        sendSpan.Parent.Should().Be(ambient, "the send span still lives in the caller's trace");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceParentHeader, out var parent).Should().BeTrue();
        parent.Should().Be(sendSpan.Id);
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out _).Should().BeFalse();
    }

    [Test]
    public async Task SendAsyncUsesParentHeaderWhenMessageTypeOptsOut()
    {
        _messageValidator.HasConsumers(typeof(NoLinkMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        using var source = new ActivitySource("test-sender-optout");
        using var ambient = source.StartActivity("job");

        DictionarySendHeaders observedHeaders = null!;
        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci => observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[1], out _));

        await _messageBus.SendAsync(typeof(NoLinkMessage), new NoLinkMessage(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG NoLinkMessage send");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceParentHeader, out var parent).Should().BeTrue();
        parent.Should().Be(sendSpan.Id, "[TraceLink(false)] overrides the enabled default");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out _).Should().BeFalse();
    }

    [Test]
    public async Task SendAsyncUsesLinkHeaderWhenMessageTypeOptsInDespiteDisabledConfig()
    {
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler,
            _deduplicateKeyProvider, _queueResolver, new MessageBusConfiguration { UseTraceLinks = false });
        _messageValidator.HasConsumers(typeof(ForceLinkMessage)).Returns(true);
        var started = new List<Activity>();
        using var listener = StartListener(started);
        using var source = new ActivitySource("test-sender-optin");
        using var ambient = source.StartActivity("job");
        ambient.Should().NotBeNull();

        DictionarySendHeaders observedHeaders = null!;
        _sendEndpoint
            .When(x => x.Send(Arg.Any<object>(), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>()))
            .Do(ci => observedHeaders = ExecutePipeOnSendContext((IPipe<SendContext>)ci[1], out _));

        await _messageBus.SendAsync(typeof(ForceLinkMessage), new ForceLinkMessage(1));

        var sendSpan = started.Single(a => a.OperationName == "MSG ForceLinkMessage send");
        observedHeaders.TryGetHeader(MessageBusConstants.TraceLinkHeader, out var link).Should().BeTrue();
        link.Should().Be(sendSpan.Id);
        observedHeaders.TryGetHeader(MessageBusConstants.TraceParentHeader, out _).Should().BeFalse();
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

    [Test]
    public async Task SendAsyncUsesExchangeUriForQuorumQueues()
    {
        var listenAddress = QueueResolver.BuildListenAddress(typeof(TestMessage));
        var quorumResolver = new QueueResolver(
            new MessageBusConfiguration { Host = "localhost", UseQuorumQueues = true },
            [(listenAddress, [typeof(TestMessage)])]);
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler, _deduplicateKeyProvider, quorumResolver, _configuration);
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        _deduplicateKeyProvider = Substitute.For<IMessageDeduplicateKeyProvider>();
        await _messageBus.SendAsync(new TestMessage(1));
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Is<Uri>(u => u.Scheme == "exchange"));
    }

    [Test]
    public async Task SendAsyncUsesQueueUriForClassicQueues()
    {
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        _deduplicateKeyProvider = Substitute.For<IMessageDeduplicateKeyProvider>();
        await _messageBus.SendAsync(new TestMessage(1));
        await _sendEndpointProvider.Received(1).GetSendEndpoint(Arg.Is<Uri>(u => u.Scheme == "queue"));
    }

    [Test]
    public async Task ScheduleAsyncUsesExchangeUriForQuorumQueues()
    {
        var listenAddress = QueueResolver.BuildListenAddress(typeof(TestMessage));
        var quorumResolver = new QueueResolver(
            new MessageBusConfiguration { Host = "localhost", UseQuorumQueues = true },
            [(listenAddress, [typeof(TestMessage)])]);
        _messageBus = new MessageSender(_dateTimeProvider, _sendEndpointProvider, _messageValidator, _scheduler, _deduplicateKeyProvider, quorumResolver, _configuration);
        _messageValidator.HasConsumers(typeof(TestMessage)).Returns(true);
        await _messageBus.ScheduleAsync(new TestMessage(1), TimeSpan.FromSeconds(1));
        await _scheduler.Received(1).ScheduleSend(Arg.Is<Uri>(u => u.Scheme == "exchange"), Arg.Any<DateTime>(), Arg.Any<object>(), typeof(TestMessage), Arg.Any<IPipe<SendContext>>(), Arg.Any<CancellationToken>());
    }

    private record TestMessage(int Id) : IMessage;

    [TraceLink(false)]
    private record NoLinkMessage(int Id) : IMessage;

    [TraceLink]
    private record ForceLinkMessage(int Id) : IMessage;
}
