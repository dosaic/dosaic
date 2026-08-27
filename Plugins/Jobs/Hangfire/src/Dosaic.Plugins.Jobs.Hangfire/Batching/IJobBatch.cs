using Dosaic.Plugins.Jobs.Hangfire.Job;
using Hangfire;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Handle to a job that was added to a batch. Use it to chain continuations onto that job
    ///     before the batch is written.
    /// </summary>
    public interface IJobBatchItem
    {
        /// <summary>1 based position inside the batch.</summary>
        int Index { get; }

        /// <summary>
        ///     Hangfire job id. Only available after the batch has been saved, and null when the job was
        ///     suppressed as a duplicate — see <see cref="IsSuppressed" />.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     True when the job was not written because <see cref="Attributes.UniquePerQueueAttribute" />
        ///     found an equivalent job. Continuations of a suppressed job are suppressed as well. Only
        ///     meaningful after the batch has been saved.
        /// </summary>
        bool IsSuppressed { get; }

        IJobBatchItem ContinueWith<TJob>(string queue = EnqueuedState.DefaultQueue,
            JobContinuationOptions options = JobContinuationOptions.OnlyOnSucceededState) where TJob : IAsyncJob;

        IJobBatchItem ContinueWith<TJob, TJobParams>(TJobParams parameters,
            string queue = EnqueuedState.DefaultQueue,
            JobContinuationOptions options = JobContinuationOptions.OnlyOnSucceededState)
            where TJob : IParameterizedAsyncJob<TJobParams>;
    }

    /// <summary>
    ///     Collects any number of jobs — including continuation chains between them — and writes all of
    ///     them to the storage in a single round trip when <see cref="Save" /> is called.
    /// </summary>
    /// <remarks>
    ///     The bulk write bypasses Hangfire's client side filter pipeline, because those filters issue
    ///     their own queries and would defeat the single round trip. Server side filters are unaffected.
    ///     <see cref="Attributes.UniquePerQueueAttribute" /> is the exception: its fingerprint claim is
    ///     folded into the bulk statement, so batched jobs are deduplicated without an extra round trip.
    /// </remarks>
    public interface IJobBatch
    {
        /// <summary>Number of jobs currently in the batch.</summary>
        int Count { get; }

        IJobBatchItem Enqueue<TJob>(string queue = EnqueuedState.DefaultQueue) where TJob : IAsyncJob;

        IJobBatchItem Enqueue<TJob, TJobParams>(TJobParams parameters, string queue = EnqueuedState.DefaultQueue)
            where TJob : IParameterizedAsyncJob<TJobParams>;

        IJobBatchItem Schedule<TJob>(TimeSpan delay, string queue = EnqueuedState.DefaultQueue) where TJob : IAsyncJob;

        IJobBatchItem Schedule<TJob, TJobParams>(TJobParams parameters, TimeSpan delay,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;

        IJobBatchItem ScheduleAt<TJob>(DateTimeOffset enqueueAt, string queue = EnqueuedState.DefaultQueue)
            where TJob : IAsyncJob;

        IJobBatchItem ScheduleAt<TJob, TJobParams>(TJobParams parameters, DateTimeOffset enqueueAt,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;

        /// <summary>
        ///     Writes the whole batch and returns the created job ids in insertion order. Jobs that were
        ///     suppressed as duplicates get a null id.
        /// </summary>
        Task<IReadOnlyList<string>> SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Writes the whole batch and returns the created job ids in insertion order. Jobs that were
        ///     suppressed as duplicates get a null id.
        /// </summary>
        IReadOnlyList<string> Save();
    }
}
