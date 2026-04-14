using System.Reflection;
using AwesomeAssertions;
using NSubstitute;
using NUnit.Framework;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;

namespace Dosaic.Plugins.Jobs.TickerQ.Tests
{
    public class JobManagerTests
    {
        private ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
        private ICronTickerManager<CronTickerEntity> _cronTickerManager;
        private IJobManager _jobManager;

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
            _cronTickerManager = Substitute.For<ICronTickerManager<CronTickerEntity>>();
            _jobManager = new JobManager(_timeTickerManager, _cronTickerManager);
        }

        [Test]
        public async Task EnqueueAsyncCreatesTimeTickerWithImmediateExecution()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.EnqueueAsync("Test");

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e => e.Function == "Test"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnqueueAsyncWithParametersSerializesRequest()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.EnqueueAsync("TestParam", "hello");

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "TestParam" && e.Request != null),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ScheduleAsyncWithDelayCreatesTimeTickerInFuture()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.ScheduleAsync("Test", TimeSpan.FromMinutes(5));

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "Test" && e.ExecutionTime > DateTime.UtcNow.AddMinutes(4)),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ScheduleAsyncWithDateTimeCreatesTimeTickerAtSpecifiedTime()
        {
            var expectedId = Guid.NewGuid();
            var scheduledTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.ScheduleAsync("Test", scheduledTime);

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "Test" && e.ExecutionTime == scheduledTime),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ScheduleAsyncWithParametersAndDelayWorks()
        {
            var expectedId = Guid.NewGuid();
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.ScheduleAsync("TestParam", "data",
                TimeSpan.FromHours(1));

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "TestParam" && e.Request != null),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ScheduleAsyncWithParametersAndDateTimeWorks()
        {
            var expectedId = Guid.NewGuid();
            var scheduledTime = new DateTime(2030, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            _timeTickerManager.AddAsync(Arg.Any<TimeTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity { Id = expectedId }));

            var result = await _jobManager.ScheduleAsync("TestParam", "data", scheduledTime);

            result.Should().Be(expectedId);
            await _timeTickerManager.Received(1).AddAsync(
                Arg.Is<TimeTickerEntity>(e =>
                    e.Function == "TestParam" && e.ExecutionTime == scheduledTime && e.Request != null),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RegisterRecurringAsyncCreatesCronTicker()
        {
            _cronTickerManager.AddAsync(Arg.Any<CronTickerEntity>(), Arg.Any<CancellationToken>())
                .Returns(CreateResult(new CronTickerEntity()));

            await _jobManager.RegisterRecurringAsync("Test", "0 * * * *");

            await _cronTickerManager.Received(1).AddAsync(
                Arg.Is<CronTickerEntity>(e =>
                    e.Function == "Test" && e.Expression == "0 * * * *" && e.IsEnabled),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteRecurringAsyncCallsCronManager()
        {
            var id = Guid.NewGuid();
            _cronTickerManager.DeleteAsync(id, Arg.Any<CancellationToken>())
                .Returns(CreateResult(new CronTickerEntity()));

            await _jobManager.DeleteRecurringAsync(id);

            await _cronTickerManager.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteAsyncCallsTimeTickerManager()
        {
            var id = Guid.NewGuid();
            _timeTickerManager.DeleteAsync(id, Arg.Any<CancellationToken>())
                .Returns(CreateResult(new TimeTickerEntity()));

            await _jobManager.DeleteAsync(id);

            await _timeTickerManager.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        }
    }
}
