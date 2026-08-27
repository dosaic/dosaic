using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Fetching
{
    public class PrefetchedJobTests
    {
        private IJobQueueClient _client;

        private PrefetchedJob GetJob(TimeSpan? sliding = null) => new(_client, 42, "7", sliding);

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
            _client.Received(1).Remove(42);
            _client.DidNotReceive().Requeue(Arg.Any<long>());
        }

        [Test]
        public void DisposingAnUnfinishedJobMakesItVisibleAgain()
        {
            GetJob().Dispose();
            _client.Received(1).Requeue(42);
        }

        [Test]
        public async Task SlidingInvisibilityKeepsTheEntryAlive()
        {
            using var job = GetJob(TimeSpan.FromMilliseconds(10));
            await Task.Delay(80);
            _client.ReceivedWithAnyArgs().KeepAlive(default);
        }
    }
}
