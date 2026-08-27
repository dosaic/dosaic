using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Fetching
{
    public class PrefetchJobStorageTests
    {
        private JobStorage _inner;
        private IJobQueueClient _client;

        private PrefetchJobStorage GetStorage(params (string Queue, int Prefetch)[] queues) =>
            new(_inner, _client,
                queues.ToDictionary(x => x.Queue, x => new PrefetchSettings
                {
                    PrefetchCount = x.Prefetch,
                    PollInterval = TimeSpan.FromMilliseconds(1)
                }),
                new PrefetchSettings { PrefetchCount = 1, PollInterval = TimeSpan.FromMilliseconds(1) });

        [SetUp]
        public void Up()
        {
            _inner = Substitute.For<JobStorage>();
            _client = Substitute.For<IJobQueueClient>();
        }

        [Test]
        public void ConnectionsAreWrappedSoFetchingGoesThroughThePrefetcher()
        {
            _inner.GetConnection().Returns(Substitute.For<JobStorageConnection>());
            _client.Fetch(Arg.Any<string[]>(), 25, Arg.Any<TimeSpan>())
                .Returns([new PrefetchedQueueEntry(1, 5)]);

            var connection = GetStorage(("bulk", 25)).GetConnection();

            connection.Should().BeOfType<PrefetchStorageConnection>();
            connection.FetchNextJob(["bulk"], CancellationToken.None).JobId.Should().Be("5");
            _client.Received(1).Fetch(Arg.Any<string[]>(), 25, Arg.Any<TimeSpan>());
        }

        [Test]
        public void UnknownQueuesFallBackToTheDefaultPrefetchCount()
        {
            _inner.GetConnection().Returns(Substitute.For<JobStorageConnection>());
            _client.Fetch(Arg.Any<string[]>(), 1, Arg.Any<TimeSpan>()).Returns([new PrefetchedQueueEntry(1, 9)]);

            GetStorage(("bulk", 25)).GetConnection().FetchNextJob(["default"], CancellationToken.None)
                .JobId.Should().Be("9");
        }

        [Test]
        public void NonStandardConnectionsAreLeftUntouched()
        {
            var raw = Substitute.For<IStorageConnection>();
            _inner.GetConnection().Returns(raw);
            GetStorage().GetConnection().Should().BeSameAs(raw);
        }

        [Test]
        public void EverythingElseIsDelegatedToTheWrappedStorage()
        {
            var storage = GetStorage();
            storage.GetMonitoringApi();
            storage.GetReadOnlyConnection();
#pragma warning disable CS0618 // the wrapped storage still implements the component based API
            storage.GetComponents();
#pragma warning restore CS0618
            storage.GetServerRequiredProcesses();
            storage.GetStorageWideProcesses();
            storage.GetStateHandlers();
            storage.HasFeature("Job.Queue");
            storage.WriteOptionsToLog(null);

            _inner.Received(1).GetMonitoringApi();
            _inner.Received(1).GetReadOnlyConnection();
#pragma warning disable CS0618
            _inner.Received(1).GetComponents();
#pragma warning restore CS0618
            _inner.Received(1).GetServerRequiredProcesses();
            _inner.Received(1).GetStorageWideProcesses();
            _inner.Received(1).GetStateHandlers();
            _inner.Received(1).HasFeature("Job.Queue");
            _inner.Received(1).WriteOptionsToLog(null);
        }
    }
}
