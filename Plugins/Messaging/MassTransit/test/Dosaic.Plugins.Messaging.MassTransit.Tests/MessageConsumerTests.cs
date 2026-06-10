using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class MessageConsumerTests
{
    public record TestMessageForConsumer : IMessage;

    private IList<IMessageConsumer<TestMessageForConsumer>> _consumers;
    private MessageConsumer<TestMessageForConsumer> _consumer;
    private FakeLogger<MessageConsumer<TestMessageForConsumer>> _logger;

    [SetUp]
    public void Setup()
    {
        _consumers = [Substitute.For<IMessageConsumer<TestMessageForConsumer>>(), Substitute.For<IMessageConsumer<TestMessageForConsumer>>()];
        _logger = new FakeLogger<MessageConsumer<TestMessageForConsumer>>();
        _consumer = new MessageConsumer<TestMessageForConsumer>(_logger, _consumers);
    }

    [Test]
    public async Task ShouldProcessOnEveryConsumer()
    {
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        var message = new TestMessageForConsumer();
        context.Message.Returns(message);
        await _consumer.Consume(context);
        foreach (var consumer in _consumers)
            await consumer.Received(1).ProcessAsync(message, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldLogErrorAndThrowOnFailure()
    {
        _consumers[0].ProcessAsync(Arg.Any<TestMessageForConsumer>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Test exception"));
        var message = new TestMessageForConsumer();
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(message);
        var exception = (await _consumer.Invoking(x => x.Consume(context)).Should().ThrowAsync<AggregateException>())
            .Which;
        exception.InnerExceptions.Should().HaveCount(1);
        exception.InnerExceptions[0].Message.Should().Be("Test exception");
        await _consumers[0].Received(1).ProcessAsync(message, Arg.Any<CancellationToken>());
        await _consumers[1].Received(1).ProcessAsync(message, Arg.Any<CancellationToken>());
        _logger.Entries.Should().Contain(x => x.Message.Contains("Could not process message with consumer") && x.Level == LogLevel.Error);
    }

    [Test]
    public async Task ShouldEmitFailureMetrics()
    {
        using var collector = new TestMetricsCollector("messaging.consumer.failures");
        _consumers[0].ProcessAsync(Arg.Any<TestMessageForConsumer>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Processing error"));
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await _consumer.Invoking(x => x.Consume(context)).Should().ThrowAsync<AggregateException>();
        collector.CollectedMetrics.Should().ContainsMetric(1, "message_type", nameof(TestMessageForConsumer));
    }

    [Test]
    public async Task ShouldEmitDurationMetrics()
    {
        using var collector = new TestMetricsCollector("messaging.consumer.duration");
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await _consumer.Consume(context);
        collector.Instruments.Should().Contain("messaging.consumer.duration");
    }

    [ConsumerTimeout(1)]
    public class TimeoutConsumer : IMessageConsumer<TestMessageForConsumer>
    {
        public async Task ProcessAsync(TestMessageForConsumer message, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    [Test]
    public async Task ShouldTimeoutConsumerWithAttribute()
    {
        var timeoutConsumers = new List<IMessageConsumer<TestMessageForConsumer>> { new TimeoutConsumer() };
        var consumer = new MessageConsumer<TestMessageForConsumer>(_logger, timeoutConsumers);
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await consumer.Invoking(x => x.Consume(context)).Should().ThrowAsync<AggregateException>();
        _logger.Entries.Should().Contain(x => x.Level == LogLevel.Error);
    }

    [Test]
    public async Task ShouldNotTimeoutConsumerWithoutAttribute()
    {
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await _consumer.Consume(context);
        foreach (var consumer in _consumers)
            await consumer.Received(1).ProcessAsync(Arg.Any<TestMessageForConsumer>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldEmitConsumerSpanPerConsumerWithOkStatus()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("TestMessageForConsumer")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await _consumer.Consume(context);

        stopped.Should().HaveCount(2);
        stopped.Should().AllSatisfy(span =>
        {
            span.Source.Name.Should().Be(Tracing.SourceName);
            span.Kind.Should().Be(ActivityKind.Consumer);
            span.Status.Should().Be(ActivityStatusCode.Ok);
            span.GetTagItem("messaging.message_type").Should().Be(nameof(TestMessageForConsumer));
        });
    }

    [Test]
    public async Task ShouldMarkConsumerSpanErrorOnFailure()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("TestMessageForConsumer")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        _consumers[0].ProcessAsync(Arg.Any<TestMessageForConsumer>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        await _consumer.Invoking(x => x.Consume(context)).Should().ThrowAsync<AggregateException>();

        stopped.Should().HaveCount(2);
        stopped.Count(s => s.Status == ActivityStatusCode.Error).Should().Be(1);
        stopped.Count(s => s.Status == ActivityStatusCode.Ok).Should().Be(1);
    }

    [Test]
    public async Task ShouldLinkConsumerSpanToSendingSpanWhenLinkHeaderPresent()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("TestMessageForConsumer")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var traceparent = $"00-{traceId}-{spanId}-01";
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        context.Headers.TryGetHeader(MessageBusConstants.TraceLinkHeader, out Arg.Any<object>())
            .Returns(ci => { ci[1] = traceparent; return true; });

        // An ambient activity must NOT be inherited as the parent: a default parentContext is ignored
        // by ActivitySource when Activity.Current is set, which would silently break the root trace.
        using var ambient = Tracing.StartActivity("ambient-job");
        ambient.Should().NotBeNull();

        await _consumer.Consume(context);

        stopped.Should().HaveCount(2);
        stopped.Should().AllSatisfy(span =>
        {
            span.Parent.Should().BeNull("link mode starts a fresh root trace");
            span.TraceId.Should().NotBe(traceId, "the consume span is its own trace, only linked to the sender");
            span.TraceId.Should().NotBe(ambient.TraceId, "the consume span must not inherit the ambient activity");
            span.TraceId.ToHexString().Should().NotBe("00000000000000000000000000000000", "the root trace id must be valid");
            span.Links.Should().ContainSingle(l => l.Context.TraceId == traceId && l.Context.SpanId == spanId);
        });
    }

    [Test]
    public async Task ShouldParentConsumerSpanToSendingSpanWhenParentHeaderPresent()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("TestMessageForConsumer")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var traceparent = $"00-{traceId}-{spanId}-01";
        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());
        context.Headers.TryGetHeader(MessageBusConstants.TraceParentHeader, out Arg.Any<object>())
            .Returns(ci => { ci[1] = traceparent; return true; });

        await _consumer.Consume(context);

        stopped.Should().HaveCount(2);
        stopped.Should().AllSatisfy(span =>
        {
            span.TraceId.Should().Be(traceId, "parent mode continues the sender's trace");
            span.ParentSpanId.Should().Be(spanId);
            span.Links.Should().BeEmpty();
        });
    }

    [Test]
    public async Task ShouldNotLinkConsumerSpanWhenLinkHeaderAbsent()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("TestMessageForConsumer")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        var context = Substitute.For<ConsumeContext<TestMessageForConsumer>>();
        context.Message.Returns(new TestMessageForConsumer());

        await _consumer.Consume(context);

        stopped.Should().HaveCount(2);
        stopped.Should().AllSatisfy(span => span.Links.Should().BeEmpty());
    }

    public record EntityChange<T>(T Payload) : IMessage;

    [Test]
    public async Task ShouldRenderGenericMessageNameInSpanAndTag()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == Tracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { if (a.OperationName.Contains("EntityChange")) stopped.Add(a); },
        };
        ActivitySource.AddActivityListener(listener);

        var consumers = new List<IMessageConsumer<EntityChange<TestMessageForConsumer>>>
        {
            Substitute.For<IMessageConsumer<EntityChange<TestMessageForConsumer>>>()
        };
        var consumer = new MessageConsumer<EntityChange<TestMessageForConsumer>>(
            new FakeLogger<MessageConsumer<EntityChange<TestMessageForConsumer>>>(), consumers);
        var context = Substitute.For<ConsumeContext<EntityChange<TestMessageForConsumer>>>();
        context.Message.Returns(new EntityChange<TestMessageForConsumer>(new TestMessageForConsumer()));

        await consumer.Consume(context);

        stopped.Should().ContainSingle();
        stopped[0].OperationName.Should().Be("MSG EntityChange<TestMessageForConsumer> consume");
        stopped[0].GetTagItem("messaging.message_type").Should().Be("EntityChange<TestMessageForConsumer>");
    }
}
