using System.Reflection;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ.Tests
{
    public class TickerQMessageSchedulerTests
    {
        private ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
        private IMessageScheduler _scheduler;

        private static TickerResult<T> CreateResult<T>(T entity) where T : class
        {
            return (TickerResult<T>)Activator.CreateInstance(
                typeof(TickerResult<T>),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { entity },
                null);
        }

        [SetUp]
        public void Setup()
        {
            _timeTickerManager = Substitute.For<ITimeTickerManager<TimeTickerEntity>>();
            var configuration = new TickerQMessageSchedulerConfiguration();
            var logger = new NullLogger<TickerQMessageScheduler>();
            _scheduler = new TickerQMessageScheduler(_timeTickerManager, configuration, logger);
        }

        [Test]
        public async Task ScheduleSendCreatesTimeTickerEntity()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var destination = new Uri("rabbitmq://localhost/test-queue");
            var scheduledTime = DateTime.UtcNow.AddMinutes(5);
            var message = new TestScheduledMessage { Content = "hello" };

            var result = await _scheduler.ScheduleSend(destination, scheduledTime, message,
                CancellationToken.None);

            result.Should().NotBeNull();
            result.TokenId.Should().Be(expectedId);
            result.ScheduledTime.Should().Be(scheduledTime);
            result.Destination.Should().Be(destination);
            result.Payload.Content.Should().Be("hello");

            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "masstransit-scheduled-send" &&
                    e.ExecutionTime == scheduledTime &&
                    e.Request != null),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ScheduleSendNonGenericCreatesTimeTickerEntity()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var destination = new Uri("rabbitmq://localhost/test-queue");
            var scheduledTime = DateTime.UtcNow.AddMinutes(10);

            var result = await _scheduler.ScheduleSend(destination, scheduledTime,
                new TestScheduledMessage { Content = "test" }, typeof(TestScheduledMessage),
                CancellationToken.None);

            result.Should().NotBeNull();
            result.TokenId.Should().Be(expectedId);
        }

        [Test]
        public async Task SchedulePublishCreatesTimeTickerEntity()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var scheduledTime = DateTime.UtcNow.AddHours(1);
            var message = new TestScheduledMessage { Content = "publish" };

            var result = await _scheduler.SchedulePublish(scheduledTime, message,
                CancellationToken.None);

            result.Should().NotBeNull();
            result.TokenId.Should().Be(expectedId);
            result.Payload.Content.Should().Be("publish");

            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "masstransit-scheduled-send" &&
                    e.ExecutionTime == scheduledTime),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SchedulePublishNonGenericCreatesTimeTickerEntity()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var scheduledTime = DateTime.UtcNow.AddHours(2);

            var result = await _scheduler.SchedulePublish(scheduledTime,
                new TestScheduledMessage { Content = "pub" }, typeof(TestScheduledMessage),
                CancellationToken.None);

            result.Should().NotBeNull();
            result.TokenId.Should().Be(expectedId);
        }

        [Test]
        public async Task CancelScheduledSendDeletesTimeTickerEntity()
        {
            var tokenId = Guid.NewGuid();
            _timeTickerManager.DeleteAsync(tokenId, Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity()));

            await _scheduler.CancelScheduledSend(new Uri("rabbitmq://localhost/q"), tokenId,
                CancellationToken.None);

            await _timeTickerManager.Received(1).DeleteAsync(tokenId, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CancelScheduledPublishDeletesTimeTickerEntity()
        {
            var tokenId = Guid.NewGuid();
            _timeTickerManager.DeleteAsync(tokenId, Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity()));

            await _scheduler.CancelScheduledPublish<TestScheduledMessage>(tokenId,
                CancellationToken.None);

            await _timeTickerManager.Received(1).DeleteAsync(tokenId, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CancelScheduledPublishByTypeDeletesTimeTickerEntity()
        {
            var tokenId = Guid.NewGuid();
            _timeTickerManager.DeleteAsync(tokenId, Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity()));

            await _scheduler.CancelScheduledPublish(typeof(TestScheduledMessage), tokenId,
                CancellationToken.None);

            await _timeTickerManager.Received(1).DeleteAsync(tokenId, Arg.Any<CancellationToken>());
        }

        public class TestScheduledMessage
        {
            public string Content { get; set; }
        }
    }
}
