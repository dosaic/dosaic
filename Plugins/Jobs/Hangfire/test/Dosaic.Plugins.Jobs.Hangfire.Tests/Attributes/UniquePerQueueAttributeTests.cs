using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Attributes
{
    public class UniquePerQueueAttributeTests
    {
        private const string Queue = "test";
        private const string JobId = "1";
        private ElectStateContext _context = null!;
        private IStorageConnection _connection = null!;
        private IWriteOnlyTransaction _transaction = null!;
        private IJobUniquenessStore _store = null!;
        private UniquePerQueueAttribute _attribute = null!;

        private void Setup(global::Hangfire.Common.Job job, bool checkRunningJobs = false,
            bool checkScheduledJobs = false, string currentState = null)
        {
            var storage = Substitute.For<JobStorage>();
            _connection = Substitute.For<IStorageConnection>();
            _transaction = Substitute.For<IWriteOnlyTransaction>();
            var applyStateContext = new ApplyStateContext(
                storage: storage,
                connection: _connection,
                transaction: _transaction,
                backgroundJob: new BackgroundJob(JobId, job, DateTime.Now),
                newState: Substitute.For<IState>(),
                oldStateName: currentState);
            _context = new ElectStateContext(applyStateContext) { CandidateState = new EnqueuedState() };
            _store = Substitute.For<IJobUniquenessStore>();
            _store.Claim(Arg.Any<IReadOnlyList<JobUniquenessClaim>>(), Arg.Any<double>()).Returns([]);
            JobUniquenessStores.Use(storage, _store);
            _attribute = new UniquePerQueueAttribute(Queue)
            {
                CheckRunningJobs = checkRunningJobs,
                CheckScheduledJobs = checkScheduledJobs
            };
        }

        private void ClaimSucceeds() =>
            _store.Claim(Arg.Any<IReadOnlyList<JobUniquenessClaim>>(), Arg.Any<double>())
                .Returns(x => x.Arg<IReadOnlyList<JobUniquenessClaim>>());

        [Test]
        public void KeepsTheJobWhenItWinsTheClaim()
        {
            Setup(CreateJob());
            ClaimSucceeds();
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().BeOfType<EnqueuedState>();
            (_context.CandidateState as EnqueuedState)!.Queue.Should().Be(Queue);
        }

        [Test]
        public void ClaimsTheFingerprintOfTheJobOnItsOwnQueue()
        {
            var job = CreateJob();
            Setup(job);
            ClaimSucceeds();
            _attribute.OnStateElection(_context);
            _store.Received(1).Claim(
                Arg.Is<IReadOnlyList<JobUniquenessClaim>>(x =>
                    x.Count == 1
                    && x[0].SetKey == JobFingerprint.SetKey(Queue)
                    && x[0].Fingerprint == JobFingerprint.Compute(job, Queue)),
                Arg.Any<double>());
        }

        [Test]
        public void RemembersTheClaimOnTheJob()
        {
            var job = CreateJob();
            Setup(job);
            ClaimSucceeds();
            _attribute.OnStateElection(_context);
            _connection.Received(1).SetJobParameter(JobId, JobFingerprint.ClaimParameterName,
                $"\"{JobFingerprint.Compute(job, Queue)}\"");
        }

        [Test]
        public void DeletesTheJobWhenItLosesTheClaim()
        {
            Setup(CreateJob());
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().BeOfType<DeletedState>();
            (_context.CandidateState as DeletedState)!.Reason.Should().Be(UniquePerQueueAttribute.DuplicateReason);
        }

        [Test]
        public void DeletesTheParameterizedJobWhenItLosesTheClaim()
        {
            Setup(CreateParameterizedJob("testPayload"));
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().BeOfType<DeletedState>();
        }

        [Test]
        public void DifferentArgumentsGetDifferentFingerprints()
        {
            JobFingerprint.Compute(CreateParameterizedJob("a"), Queue)
                .Should().NotBe(JobFingerprint.Compute(CreateParameterizedJob("b"), Queue));
        }

        [Test]
        public void EqualArgumentsGetTheSameFingerprint()
        {
            JobFingerprint.Compute(CreateParameterizedJob("a"), Queue)
                .Should().Be(JobFingerprint.Compute(CreateParameterizedJob("a"), Queue));
        }

        [Test]
        public void DifferentQueuesGetDifferentFingerprints()
        {
            var job = CreateJob();
            JobFingerprint.Compute(job, Queue).Should().NotBe(JobFingerprint.Compute(job, "other"));
        }

        [Test]
        public void DoesNotClaimForScheduledJobsByDefault()
        {
            Setup(CreateJob());
            var state = new ScheduledState(TimeSpan.FromMilliseconds(100));
            _context.CandidateState = state;
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().Be(state);
            _store.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void ClaimsForScheduledJobsWhenScheduledJobsAreChecked()
        {
            Setup(CreateJob(), checkScheduledJobs: true);
            _context.CandidateState = new ScheduledState(TimeSpan.FromMilliseconds(100));
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().BeOfType<DeletedState>();
        }

        [Test]
        public void IgnoresStatesThatNeitherEnqueueNorSchedule()
        {
            Setup(CreateJob(), checkScheduledJobs: true);
            var state = Substitute.For<IState>();
            state.Name.Returns(ProcessingState.StateName);
            _context.CandidateState = state;
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().Be(state);
            _store.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void DoesNotLetAJobLoseAgainstItsOwnClaim()
        {
            var job = CreateJob();
            Setup(job, checkScheduledJobs: true, currentState: ScheduledState.StateName);
            _connection.GetJobParameter(JobId, JobFingerprint.ClaimParameterName)
                .Returns($"\"{JobFingerprint.Compute(job, Queue)}\"");
            _attribute.OnStateElection(_context);
            _context.CandidateState.Should().BeOfType<EnqueuedState>();
            _store.DidNotReceiveWithAnyArgs().Claim(default, default);
        }

        [Test]
        public void DoesNotReadTheClaimParameterForNewlyCreatedJobs()
        {
            Setup(CreateJob());
            ClaimSucceeds();
            _attribute.OnStateElection(_context);
            _connection.DidNotReceive().GetJobParameter(JobId, JobFingerprint.ClaimParameterName);
        }

        [TestCase("Succeeded")]
        [TestCase("Deleted")]
        [TestCase("Failed")]
        [TestCase("Processing")]
        public void ReleasesTheClaimWhenTheJobLeavesTheCheckedStates(string stateName)
        {
            var job = CreateJob();
            Setup(job);
            var fingerprint = JobFingerprint.Compute(job, Queue);
            _connection.GetJobParameter(JobId, JobFingerprint.ClaimParameterName).Returns($"\"{fingerprint}\"");
            _attribute.OnStateApplied(ApplyContext(job, stateName), _transaction);
            _transaction.Received(1).RemoveFromSet(JobFingerprint.SetKey(Queue), fingerprint);
            _connection.Received(1).SetJobParameter(JobId, JobFingerprint.ClaimParameterName, null);
        }

        [Test]
        public void KeepsTheClaimWhileTheJobIsProcessingWhenRunningJobsAreChecked()
        {
            var job = CreateJob();
            Setup(job, checkRunningJobs: true);
            _connection.GetJobParameter(JobId, JobFingerprint.ClaimParameterName)
                .Returns($"\"{JobFingerprint.Compute(job, Queue)}\"");
            _attribute.OnStateApplied(ApplyContext(job, ProcessingState.StateName), _transaction);
            _transaction.DidNotReceiveWithAnyArgs().RemoveFromSet(default, default);
        }

        [Test]
        public void DoesNotReleaseAClaimTheJobNeverOwned()
        {
            var job = CreateJob();
            Setup(job);
            _attribute.OnStateApplied(ApplyContext(job, DeletedState.StateName), _transaction);
            _transaction.DidNotReceiveWithAnyArgs().RemoveFromSet(default, default);
        }

        [Test]
        public void OnStateUnappliedDoesNothing()
        {
            var job = CreateJob();
            Setup(job);
            _attribute.OnStateUnapplied(ApplyContext(job, DeletedState.StateName), _transaction);
            _transaction.ReceivedCalls().Should().BeEmpty();
        }

        private ApplyStateContext ApplyContext(global::Hangfire.Common.Job job, string stateName)
        {
            var newState = Substitute.For<IState>();
            newState.Name.Returns(stateName);
            return new ApplyStateContext(
                storage: Substitute.For<JobStorage>(),
                connection: _connection,
                transaction: _transaction,
                backgroundJob: new BackgroundJob(JobId, job, DateTime.Now),
                newState: newState,
                oldStateName: null);
        }

        private static global::Hangfire.Common.Job CreateJob() =>
            new(typeof(TestJob), typeof(TestJob).GetMethod(nameof(TestJob.ExecuteAsync)), CancellationToken.None);

        private static global::Hangfire.Common.Job CreateParameterizedJob(string payloadName) =>
            new(typeof(ParameterizedTestJob),
                typeof(ParameterizedTestJob).GetMethod(nameof(ParameterizedTestJob.ExecuteAsync)),
                new TestJobPayload { Name = payloadName }, CancellationToken.None);

        [ExcludeFromCodeCoverage]
        private class TestJob : IAsyncJob
        {
            public void Dispose() => GC.SuppressFinalize(this);

            public Task<object> ExecuteAsync(CancellationToken jobCancellationToken)
            {
                object x = new { success = true };
                return Task.FromResult(x);
            }
        }

        [ExcludeFromCodeCoverage]
        private class ParameterizedTestJob : IParameterizedAsyncJob<TestJobPayload>
        {
            public Task<object> ExecuteAsync(TestJobPayload value, CancellationToken jobCancellationToken)
            {
                object x = new { success = true };
                return Task.FromResult(x);
            }

            public void Dispose() => GC.SuppressFinalize(this);
        }

        [ExcludeFromCodeCoverage]
        private class TestJobPayload
        {
            public string Name { get; set; }
        }
    }
}
