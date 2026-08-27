using Hangfire;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Fallback dispatcher for storages without a bulk implementation (in-memory storage, custom
    ///     configurator storages). Creates the jobs one by one through the regular Hangfire client, so the
    ///     batch API keeps working — just without the single round trip guarantee.
    /// </summary>
    /// <remarks>
    ///     Because the jobs go through the regular client, <see cref="Attributes.UniquePerQueueAttribute" />
    ///     runs as a normal filter here. A job it deletes as a duplicate still gets an id back — only
    ///     duplicates inside the batch itself are reported as suppressed.
    /// </remarks>
    internal sealed class BackgroundJobClientBatchDispatcher : IJobBatchDispatcher
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public BackgroundJobClientBatchDispatcher(IBackgroundJobClient backgroundJobClient) =>
            _backgroundJobClient = backgroundJobClient;

        public Task<IReadOnlyList<string>> DispatchAsync(IReadOnlyList<BatchJobEntry> entries,
            CancellationToken cancellationToken = default)
        {
            var ids = new string[entries.Count];
            foreach (var entry in entries.OrderBy(x => x.Index))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // an earlier entry of the same batch already claims this fingerprint — the filter pipeline
                // cannot see that, because that job does not exist in the storage yet
                if (entry.UniqueDuplicate) continue;
                var state = entry.State;
                var job = WithQueue(entry);
                if (entry.ParentIndex.HasValue)
                {
                    var parentId = ids[entry.ParentIndex.Value - 1];
                    if (parentId is null) continue;
                    if (state is AwaitingState awaiting)
                        state = new AwaitingState(parentId, awaiting.NextState, awaiting.Options);
                }

                ids[entry.Index - 1] = _backgroundJobClient.Create(job, state);
            }

            return Task.FromResult<IReadOnlyList<string>>(ids);
        }

        /// <summary>
        ///     A scheduled entry carries its queue in <see cref="BatchJobEntry.SetValuePrefix" />, because the
        ///     bulk statement writes the schedule set itself. Here the state handler writes that set, and it
        ///     reads the queue off the job - without it the delayed job scheduler would enqueue to default.
        /// </summary>
        private static global::Hangfire.Common.Job WithQueue(BatchJobEntry entry)
        {
            if (entry.State is not ScheduledState || entry.SetValuePrefix is null) return entry.Job;
            if (entry.Job.Queue is not null) return entry.Job;
            return new global::Hangfire.Common.Job(entry.Job.Type, entry.Job.Method, entry.Job.Args,
                entry.SetValuePrefix);
        }
    }
}
