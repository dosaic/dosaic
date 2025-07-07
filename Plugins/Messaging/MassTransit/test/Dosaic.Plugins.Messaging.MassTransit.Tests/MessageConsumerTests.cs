using AwesomeAssertions;
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Testing.NUnit.Assertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Polly.Utilities;

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
        SystemClock.SleepAsync = (_, _) => Task.CompletedTask;
    }

    [TearDown]
    public void Down()
    {
        SystemClock.Reset();
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
    public async Task ProcessAsyncRetriesAndLogsError()
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
        await _consumers[0].Received(3).ProcessAsync(message, Arg.Any<CancellationToken>());
        await _consumers[1].Received(1).ProcessAsync(message, Arg.Any<CancellationToken>());
        _logger.Entries.Should().Contain(x => x.Message.Contains("Could not process message with consumer") && x.Level == LogLevel.Error);
    }
}
