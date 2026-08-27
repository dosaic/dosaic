using System.Globalization;
using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using Hangfire.States;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Batching
{
    public class JobBatchTests
    {
        private IJobBatchDispatcher _dispatcher;
        private IReadOnlyList<BatchJobEntry> _dispatched;

        private JobBatch GetBatch() => new(_dispatcher);

        [SetUp]
        public void Up()
        {
            _dispatched = null;
            _dispatcher = Substitute.For<IJobBatchDispatcher>();
            _dispatcher.DispatchAsync(Arg.Any<IReadOnlyList<BatchJobEntry>>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    _dispatched = call.Arg<IReadOnlyList<BatchJobEntry>>();
                    return Task.FromResult<IReadOnlyList<string>>(
                        Enumerable.Range(1, _dispatched.Count).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList());
                });
        }

        [Test]
        public async Task EnqueueAddsAnEnqueuedStateWithTheQueue()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>("critical");
            batch.Enqueue<TestParamJob, string>("payload", "critical");
            var ids = await batch.SaveAsync();

            ids.Should().Equal("1", "2");
            _dispatched.Should().HaveCount(2);
            _dispatched[0].State.Should().BeOfType<EnqueuedState>();
            _dispatched[0].Queue.Should().Be("critical");
            _dispatched[0].SetKey.Should().BeNull();
            _dispatched[0].Job.Type.Should().Be(typeof(TestJob));
            _dispatched[1].Job.Args.Should().Contain("payload");
        }

        [Test]
        public async Task ScheduleUsesTheScheduleSetWithTheQueuePrefix()
        {
            var batch = GetBatch();
            batch.Schedule<TestJob>(TimeSpan.FromMinutes(5), "critical");
            await batch.SaveAsync();

            var entry = _dispatched[0];
            entry.State.Should().BeOfType<ScheduledState>();
            entry.Queue.Should().BeNull();
            entry.SetKey.Should().Be("schedule");
            entry.SetValuePrefix.Should().Be("critical");
            entry.SetScore.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task ContinuationsReferenceTheirAntecedentByIndex()
        {
            var batch = GetBatch();
            var first = batch.Enqueue<TestJob>();
            var second = first.ContinueWith<TestParamJob, string>("payload", "critical");
            second.ContinueWith<TestJob>(options: JobContinuationOptions.OnAnyFinishedState);
            await batch.SaveAsync();

            _dispatched.Should().HaveCount(3);
            _dispatched[0].ParentIndex.Should().BeNull();
            _dispatched[1].ParentIndex.Should().Be(1);
            _dispatched[1].SetKey.Should().Be("awaiting");
            _dispatched[1].SetValuePrefix.Should().BeNull();
            _dispatched[1].ContinuationOptions.Should().Be(JobContinuationOptions.OnlyOnSucceededState);
            _dispatched[2].ParentIndex.Should().Be(2);
            _dispatched[2].ContinuationOptions.Should().Be(JobContinuationOptions.OnAnyFinishedState);
        }

        [Test]
        public async Task ContinuationsCarryTheQueueInsideTheNextState()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>().ContinueWith<TestJob>("critical");
            await batch.SaveAsync();

            var awaiting = (AwaitingState)_dispatched[1].State;
            awaiting.ParentId.Should().Be(JobBatch.PendingParentId);
            ((EnqueuedState)awaiting.NextState).Queue.Should().Be("critical");
        }

        [Test]
        public async Task OneJobCanFanOutIntoSeveralContinuations()
        {
            var batch = GetBatch();
            var root = batch.Enqueue<TestJob>();
            root.ContinueWith<TestJob>("a");
            root.ContinueWith<TestJob>("b", JobContinuationOptions.OnAnyFinishedState);
            await batch.SaveAsync();

            _dispatched.Should().HaveCount(3);
            _dispatched.Skip(1).Should().AllSatisfy(x => x.ParentIndex.Should().Be(1));
            _dispatched[1].ContinuationOptions.Should().Be(JobContinuationOptions.OnlyOnSucceededState);
            _dispatched[2].ContinuationOptions.Should().Be(JobContinuationOptions.OnAnyFinishedState);
        }

        [Test]
        public async Task ScheduledJobsCanBeChainedToo()
        {
            var batch = GetBatch();
            batch.Schedule<TestJob>(TimeSpan.FromMinutes(5), "bulk").ContinueWith<TestJob>("bulk");
            await batch.SaveAsync();

            _dispatched[0].SetKey.Should().Be("schedule");
            _dispatched[1].SetKey.Should().Be("awaiting");
            _dispatched[1].ParentIndex.Should().Be(1);
        }

        [Test]
        public async Task EnqueueScheduleAndChainCanBeMixedInOneBatch()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>("mixed");
            batch.Schedule<TestParamJob, string>("later", TimeSpan.FromHours(1), "mixed");
            batch.ScheduleAt<TestJob>(DateTimeOffset.UtcNow.AddDays(1), "mixed");
            batch.Enqueue<TestJob>("mixed").ContinueWith<TestParamJob, string>("after", "mixed");
            await batch.SaveAsync();

            _dispatched.Select(x => x.State.Name).Should()
                .Equal("Enqueued", "Scheduled", "Scheduled", "Enqueued", "Awaiting");
            await _dispatcher.Received(1)
                .DispatchAsync(Arg.Any<IReadOnlyList<BatchJobEntry>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task BatchItemsExposeTheirIdAfterSaving()
        {
            var batch = GetBatch();
            var first = batch.Enqueue<TestJob>();
            var second = batch.Enqueue<TestJob>();
            first.Id.Should().BeNull();
            await batch.SaveAsync();
            first.Id.Should().Be("1");
            second.Id.Should().Be("2");
        }

        [Test]
        public async Task EmptyBatchesDoNotHitTheDispatcher()
        {
            var ids = await GetBatch().SaveAsync();
            ids.Should().BeEmpty();
            await _dispatcher.DidNotReceiveWithAnyArgs()
                .DispatchAsync(Arg.Any<IReadOnlyList<BatchJobEntry>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task BatchesCannotBeSavedTwice()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>();
            await batch.SaveAsync();
            await batch.Invoking(x => x.SaveAsync()).Should().ThrowAsync<InvalidOperationException>();
            batch.Invoking(x => x.Enqueue<TestJob>()).Should().Throw<InvalidOperationException>();
        }

        [Test]
        public async Task DispatcherResultCountMismatchIsRejected()
        {
            _dispatcher.DispatchAsync(Arg.Any<IReadOnlyList<BatchJobEntry>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<string>>(["1"]));
            var batch = GetBatch();
            batch.Enqueue<TestJob>();
            batch.Enqueue<TestJob>();
            await batch.Invoking(x => x.SaveAsync()).Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task CultureIsCapturedAsJobParameters()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>();
            await batch.SaveAsync();
            _dispatched[0].Parameters.Keys.Should().Contain("CurrentCulture");
            _dispatched[0].Parameters["CurrentCulture"].Should().StartWith("\"").And.EndWith("\"");
        }
        [Test]
        public async Task UniqueJobsCarryAFingerprintClaimOnTheAttributeQueue()
        {
            var batch = GetBatch();
            batch.Enqueue<UniqueTestJob>("ignored");
            await batch.SaveAsync();

            var entry = _dispatched[0];
            entry.Queue.Should().Be("unique");
            (entry.State as EnqueuedState)!.Queue.Should().Be("unique");
            entry.UniqueSetKey.Should().Be(JobFingerprint.SetKey("unique"));
            entry.UniqueFingerprint.Should().Be(JobFingerprint.Compute(entry.Job, "unique"));
            entry.UniqueDuplicate.Should().BeFalse();
            entry.UniqueExpiresAt.Should().BeGreaterThan(JobFingerprint.ToTimestamp(DateTime.UtcNow));
            entry.Parameters[JobFingerprint.ClaimParameterName].Should().Be($"\"{entry.UniqueFingerprint}\"");
        }

        [Test]
        public async Task JobsWithoutTheAttributeCarryNoClaim()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>();
            await batch.SaveAsync();

            _dispatched[0].UniqueSetKey.Should().BeNull();
            _dispatched[0].UniqueFingerprint.Should().BeNull();
            _dispatched[0].Parameters.Should().NotContainKey(JobFingerprint.ClaimParameterName);
        }

        [Test]
        public async Task OnlyTheFirstOccurrenceOfAFingerprintClaimsItInsideOneBatch()
        {
            var batch = GetBatch();
            batch.Enqueue<UniqueTestJob>();
            batch.Enqueue<UniqueTestJob>();
            batch.Enqueue<UniqueParamTestJob, string>("a");
            batch.Enqueue<UniqueParamTestJob, string>("b");
            await batch.SaveAsync();

            _dispatched[0].UniqueDuplicate.Should().BeFalse();
            _dispatched[1].UniqueDuplicate.Should().BeTrue();
            _dispatched[1].UniqueFingerprint.Should().BeNull();
            _dispatched[1].Parameters.Should().NotContainKey(JobFingerprint.ClaimParameterName);
            _dispatched[2].UniqueDuplicate.Should().BeFalse();
            _dispatched[3].UniqueDuplicate.Should().BeFalse();
        }

        [Test]
        public async Task ScheduledUniqueJobsOnlyClaimWhenScheduledJobsAreChecked()
        {
            var batch = GetBatch();
            batch.Schedule<UniqueTestJob>(TimeSpan.FromMinutes(5));
            batch.Schedule<UniqueScheduledTestJob>(TimeSpan.FromMinutes(5));
            await batch.SaveAsync();

            _dispatched[0].UniqueFingerprint.Should().BeNull();
            _dispatched[0].SetValuePrefix.Should().Be("unique");
            _dispatched[1].UniqueFingerprint.Should().NotBeNull();
        }

        [Test]
        public async Task ContinuationsAreLeftToTheFilterPipeline()
        {
            var batch = GetBatch();
            batch.Enqueue<TestJob>().ContinueWith<UniqueTestJob>("ignored");
            await batch.SaveAsync();

            var continuation = _dispatched[1];
            continuation.UniqueFingerprint.Should().BeNull();
            ((continuation.State as AwaitingState)!.NextState as EnqueuedState)!.Queue.Should().Be("unique");
        }

        [Test]
        public async Task SuppressedJobsGetNoIdAndAreReported()
        {
            _dispatcher.DispatchAsync(Arg.Any<IReadOnlyList<BatchJobEntry>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<string>>(["1", null]));
            var batch = GetBatch();
            var first = batch.Enqueue<UniqueTestJob>();
            var second = batch.Enqueue<UniqueTestJob>();
            var ids = await batch.SaveAsync();

            ids.Should().Equal("1", null);
            first.IsSuppressed.Should().BeFalse();
            second.IsSuppressed.Should().BeTrue();
            second.Id.Should().BeNull();
        }
    }
}
