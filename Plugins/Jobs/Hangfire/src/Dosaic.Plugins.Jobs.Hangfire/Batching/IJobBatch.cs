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

        /// <summary>Hangfire job id. Only available after the batch has been saved.</summary>
        string Id { get; }

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
    ///     The bulk write bypasses Hangfire's client side filter pipeline (for example
    ///     <see cref="Attributes.UniquePerQueueAttribute" />), because those filters issue their own
    ///     queries and would defeat the single round trip. Server side filters are unaffected.
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

        /// <summary>Writes the whole batch and returns the created job ids in insertion order.</summary>
        Task<IReadOnlyList<string>> SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>Writes the whole batch and returns the created job ids in insertion order.</summary>
        IReadOnlyList<string> Save();
    }
}
