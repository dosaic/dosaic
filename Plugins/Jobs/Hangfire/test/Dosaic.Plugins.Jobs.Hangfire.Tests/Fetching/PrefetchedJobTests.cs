using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Fetching
{
    public class PrefetchedJobTests
    {
        private static readonly DateTime _fetchedAt = new(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc);
        private IJobQueueClient _client;

        private PrefetchedJob GetJob(TimeSpan? sliding = null) =>
            new(_client, new PrefetchedQueueEntry(42, 7, _fetchedAt), sliding);

        [SetUp]
        public void Up() => _client = Substitute.For<IJobQueueClient>();

        [Test]
        public void RemovingTakesTheEntryOutOfTheQueueExactlyOnce()
        {
            var job = GetJob();
            job.JobId.Should().Be("7");
            job.RemoveFromQueue();
            job.RemoveFromQueue();
            job.Dispose();
            _client.Received(1).Remove(42, _fetchedAt);
            _client.DidNotReceive().Requeue(Arg.Any<long>(), Arg.Any<DateTime>());
        }

        [Test]
        public void DisposingAnUnfinishedJobMakesItVisibleAgain()
        {
            GetJob().Dispose();
            _client.Received(1).Requeue(42, _fetchedAt);
        }

        [Test]
        public async Task SlidingInvisibilityKeepsTheEntryAlive()
        {
            var renewed = _fetchedAt.AddMinutes(1);
            _client.KeepAlive(42, Arg.Any<DateTime>()).Returns(renewed);
            using var job = GetJob(TimeSpan.FromMilliseconds(10));
            await Task.Delay(80);
            _client.Received().KeepAlive(42, _fetchedAt);
            // every renewal moves the timestamp the next statement has to match on
            _client.Received().KeepAlive(42, renewed);
        }

        [Test]
        public async Task ATakenOverEntryIsNeverReleasedAgain()
        {
            _client.KeepAlive(42, Arg.Any<DateTime>()).Returns((DateTime?)null);
            var job = GetJob(TimeSpan.FromMilliseconds(10));
            await Task.Delay(80);

            job.Dispose();

            _client.DidNotReceiveWithAnyArgs().Requeue(default, default);
        }

        [Test]
        public void ReleasingWaitsForARunningKeepAlive()
        {
            var started = new ManualResetEventSlim(false);
            _client.KeepAlive(42, Arg.Any<DateTime>()).Returns(_ =>
            {
                started.Set();
                Thread.Sleep(150);
                return _fetchedAt.AddMinutes(1);
            });
            var job = GetJob(TimeSpan.FromMilliseconds(10));
            started.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            job.Dispose();

            // the requeue has to match the timestamp the keep-alive left behind, not the one it replaced
            _client.Received(1).Requeue(42, _fetchedAt.AddMinutes(1));
        }
    }
}
