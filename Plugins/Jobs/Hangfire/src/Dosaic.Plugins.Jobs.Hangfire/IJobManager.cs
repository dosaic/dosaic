using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    public interface IJobManager
    {
        IStorageConnection Connection { get; }
        IMonitoringApi MonitoringApi { get; }
        IList<RecurringJobDto> GetRecurringJobs();
        IList<string> GetQueues();
        IList<EnqueuedJobDto> GetEnqueuedJobs();
        IList<ProcessingJobDto> GetProcessingJobs();
        IList<FailedJobDto> GetFailedJobs();
        IList<FetchedJobDto> GetFetchedJobs();
        IList<RecurringJobDto> GetRecurringJobs<T>(Predicate<RecurringJobDto> predicate = null);
        IList<FetchedJobDto> GetFetchedJobs<T>(Predicate<FetchedJobDto> predicate = null);
        IList<EnqueuedJobDto> GetEnqueuedJobs<T>(Predicate<EnqueuedJobDto> predicate = null);
        IList<ProcessingJobDto> GetProcessingJobs<T>(Predicate<ProcessingJobDto> predicate = null);
        IList<FailedJobDto> GetFailedJobs<T>(Predicate<FailedJobDto> predicate = null);
        IList<JobEntity> GetJobs(Predicate<JobEntity> predicate = null);

        string Enqueue<TJob, TJobParams>(TJobParams parameters, string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;
        string Enqueue<TJob>(string queue = EnqueuedState.DefaultQueue) where TJob : IAsyncJob;
        string Schedule<TJob>(TimeSpan schedule, string queue = EnqueuedState.DefaultQueue) where TJob : IAsyncJob;
        string Schedule<TJob, TJobParams>(TJobParams parameters, TimeSpan schedule, string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;
        void RegisterRecurring<TJob>(string cron, string queue = EnqueuedState.DefaultQueue, string jobSuffix = "") where TJob : IAsyncJob;
        void RegisterRecurring<TJob, TJobParams>(TJobParams parameters, string cron, string queue = EnqueuedState.DefaultQueue, string jobSuffix = "") where TJob : IParameterizedAsyncJob<TJobParams>;
        /// <summary>
        ///     Starts a new batch. Every job added to it — including continuation chains — is written to the
        ///     storage in a single round trip when the batch is saved.
        /// </summary>
        IJobBatch CreateBatch();

        /// <summary>Enqueues one job per parameter set in a single round trip.</summary>
        IReadOnlyList<string> EnqueueBatch<TJob, TJobParams>(IEnumerable<TJobParams> parameters,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;

        /// <summary>Enqueues one job per parameter set in a single round trip.</summary>
        Task<IReadOnlyList<string>> EnqueueBatchAsync<TJob, TJobParams>(IEnumerable<TJobParams> parameters,
            string queue = EnqueuedState.DefaultQueue, CancellationToken cancellationToken = default)
            where TJob : IParameterizedAsyncJob<TJobParams>;

        /// <summary>Schedules one job per parameter set in a single round trip.</summary>
        IReadOnlyList<string> ScheduleBatch<TJob, TJobParams>(IEnumerable<TJobParams> parameters, TimeSpan schedule,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams>;

        /// <summary>Schedules one job per parameter set in a single round trip.</summary>
        Task<IReadOnlyList<string>> ScheduleBatchAsync<TJob, TJobParams>(IEnumerable<TJobParams> parameters,
            TimeSpan schedule, string queue = EnqueuedState.DefaultQueue,
            CancellationToken cancellationToken = default) where TJob : IParameterizedAsyncJob<TJobParams>;

        /// <summary>Schedules one job per parameter set for an absolute point in time, in a single round trip.</summary>
        Task<IReadOnlyList<string>> ScheduleBatchAtAsync<TJob, TJobParams>(IEnumerable<TJobParams> parameters,
            DateTimeOffset enqueueAt, string queue = EnqueuedState.DefaultQueue,
            CancellationToken cancellationToken = default) where TJob : IParameterizedAsyncJob<TJobParams>;

        void DeleteRecurring(string id);
        void Delete(string id);
    }
}
