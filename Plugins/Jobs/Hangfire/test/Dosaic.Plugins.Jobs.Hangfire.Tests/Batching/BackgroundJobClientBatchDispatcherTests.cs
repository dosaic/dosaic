using System.Globalization;
using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Hangfire;
using Hangfire.States;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Batching
{
    public class BackgroundJobClientBatchDispatcherTests
    {
        [Test]
        public async Task JobsAreCreatedInOrderAndContinuationsGetTheRealParentId()
        {
            var client = Substitute.For<IBackgroundJobClient>();
            var created = 0;
            client.Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<IState>())
                .Returns(_ => (++created).ToString(CultureInfo.InvariantCulture));

            var batch = new JobBatch(new BackgroundJobClientBatchDispatcher(client));
            batch.Enqueue<TestJob>().ContinueWith<TestJob>("critical");
            var ids = await batch.SaveAsync();

            ids.Should().Equal("1", "2");
            client.Received(1).Create(Arg.Any<global::Hangfire.Common.Job>(),
                Arg.Is<IState>(x => x is AwaitingState && ((AwaitingState)x).ParentId == "1"));
        }
        [Test]
        public async Task DuplicatesInsideTheBatchAreNotCreatedAtAll()
        {
            var client = Substitute.For<IBackgroundJobClient>();
            var created = 0;
            client.Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<IState>())
                .Returns(_ => (++created).ToString(CultureInfo.InvariantCulture));

            var batch = new JobBatch(new BackgroundJobClientBatchDispatcher(client));
            batch.Enqueue<UniqueTestJob>();
            batch.Enqueue<UniqueTestJob>();
            var ids = await batch.SaveAsync();

            ids.Should().Equal("1", null);
            client.Received(1).Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<IState>());
        }

        [Test]
        public async Task ContinuationsOfASuppressedJobAreSuppressedAsWell()
        {
            var client = Substitute.For<IBackgroundJobClient>();
            var created = 0;
            client.Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<IState>())
                .Returns(_ => (++created).ToString(CultureInfo.InvariantCulture));

            var batch = new JobBatch(new BackgroundJobClientBatchDispatcher(client));
            batch.Enqueue<UniqueTestJob>();
            batch.Enqueue<UniqueTestJob>().ContinueWith<TestJob>();
            var ids = await batch.SaveAsync();

            ids.Should().Equal("1", null, null);
            client.Received(1).Create(Arg.Any<global::Hangfire.Common.Job>(), Arg.Any<IState>());
        }
    }
}
