using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Attributes
{
    /// <summary>
    ///     Keeps at most one instance of the same job with the same arguments on a queue.
    /// </summary>
    /// <remarks>
    ///     The job's fingerprint is claimed in the storage when the job is enqueued and released again when
    ///     it leaves the checked states. Claiming is a single upsert against a unique index, so the check
    ///     costs one round trip regardless of how deep the queue is, and two clients racing for the same
    ///     fingerprint can never both win.
    /// </remarks>
    public class UniquePerQueueAttribute : JobFilterAttribute, IElectStateFilter, IApplyStateFilter
    {
        internal const string DuplicateReason = "Instance of the same job is already queued.";

        public string Queue { get; set; }

        /// <summary>Also block while an equivalent job is only scheduled, not enqueued yet.</summary>
        public bool CheckScheduledJobs { get; set; }

        /// <summary>Also block while an equivalent job is already being processed.</summary>
        public bool CheckRunningJobs { get; set; }

        /// <summary>
        ///     Safety net for claims that were never released because the owning process died. Once it has
        ///     elapsed the claim can be taken over by the next job with the same fingerprint. Only honoured
        ///     by the PostgreSQL storage.
        /// </summary>
        public int ClaimTimeoutInMinutes { get; set; } = 24 * 60;

        public UniquePerQueueAttribute(string queue)
        {
            Queue = queue;
            Order = 10;
        }

        public void OnStateElection(ElectStateContext context)
        {
            if (!IsClaimPoint(context.CandidateState)) return;
            if (context.CandidateState is EnqueuedState enqueuedState) enqueuedState.Queue = Queue;

            var fingerprint = JobFingerprint.Compute(context.BackgroundJob.Job, Queue);
            // a job that claimed the fingerprint while it was scheduled must not lose against itself
            if (OwnsClaim(context, fingerprint)) return;

            var now = JobFingerprint.ToTimestamp(DateTime.UtcNow);
            var claim = new JobUniquenessClaim(JobFingerprint.SetKey(Queue), fingerprint,
                now + TimeSpan.FromMinutes(ClaimTimeoutInMinutes).TotalSeconds);
            if (JobUniquenessStores.For(context.Storage).Claim([claim], now).Count > 0)
            {
                context.SetJobParameter(JobFingerprint.ClaimParameterName, fingerprint);
                return;
            }

            context.CandidateState = new DeletedState { Reason = DuplicateReason };
        }

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            if (!IsReleasePoint(context.NewState)) return;
            var fingerprint = context.GetJobParameter<string>(JobFingerprint.ClaimParameterName);
            // jobs that were deleted as duplicates never owned the claim and must not release someone else's
            if (string.IsNullOrEmpty(fingerprint)) return;
            transaction.RemoveFromSet(JobFingerprint.SetKey(Queue), fingerprint);
            context.Connection.SetJobParameter(context.BackgroundJob.Id, JobFingerprint.ClaimParameterName, null);
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            // nothing to do here
        }

        private bool IsClaimPoint(IState candidateState) => candidateState switch
        {
            EnqueuedState => true,
            ScheduledState => CheckScheduledJobs,
            _ => false
        };

        private bool IsReleasePoint(IState newState) =>
            newState.Name == SucceededState.StateName
            || newState.Name == DeletedState.StateName
            || newState.Name == FailedState.StateName
            || (!CheckRunningJobs && newState.Name == ProcessingState.StateName);

        /// <summary>
        ///     A freshly created job cannot own a claim yet, which keeps the parameter read off the hot path.
        /// </summary>
        private static bool OwnsClaim(ElectStateContext context, string fingerprint) =>
            context.CurrentState is not null &&
            context.GetJobParameter<string>(JobFingerprint.ClaimParameterName) == fingerprint;
    }
}
