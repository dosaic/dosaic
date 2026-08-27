using Hangfire;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Storage agnostic description of a single job inside a batch.
    /// </summary>
    public sealed class BatchJobEntry
    {
        /// <summary>1 based position inside the batch.</summary>
        public int Index { get; init; }

        public global::Hangfire.Common.Job Job { get; init; }

        /// <summary>The state the job is created in (enqueued, scheduled or awaiting).</summary>
        public IState State { get; init; }

        /// <summary>Queue the job is pushed to, or null when the state does not enqueue immediately.</summary>
        public string Queue { get; init; }

        /// <summary>Hangfire set the state handler would add the job to ("schedule", "awaiting") or null.</summary>
        public string SetKey { get; init; }

        /// <summary>Score used for <see cref="SetKey" />.</summary>
        public double SetScore { get; init; }

        /// <summary>
        ///     Optional prefix the state handler puts in front of the job id when adding it to
        ///     <see cref="SetKey" /> (Hangfire stores scheduled jobs as "queue:jobId").
        /// </summary>
        public string SetValuePrefix { get; init; }

        /// <summary>1 based index of the antecedent job inside the same batch, or null.</summary>
        public int? ParentIndex { get; init; }

        /// <summary>Continuation options, only meaningful when <see cref="ParentIndex" /> is set.</summary>
        public JobContinuationOptions ContinuationOptions { get; init; }

        /// <summary>
        ///     Hangfire set the uniqueness claim is written to, or null when the job is not deduplicated.
        /// </summary>
        public string UniqueSetKey { get; init; }

        /// <summary>
        ///     Fingerprint that has to be claimed before this job may be written, or null when the job is
        ///     not deduplicated or when an earlier entry of the same batch already claims it.
        /// </summary>
        public string UniqueFingerprint { get; init; }

        /// <summary>Unix seconds after which an unreleased claim may be taken over by another job.</summary>
        public double UniqueExpiresAt { get; init; }

        /// <summary>
        ///     True when an earlier entry of the same batch already claims this job's fingerprint. The
        ///     storage cannot detect that, because a claim conflicts only with rows that are already
        ///     committed, not with rows the same statement inserts.
        /// </summary>
        public bool UniqueDuplicate { get; init; }

        public IDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    }
}
