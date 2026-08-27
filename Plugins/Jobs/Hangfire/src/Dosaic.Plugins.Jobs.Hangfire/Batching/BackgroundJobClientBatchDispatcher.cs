using Hangfire;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Fallback dispatcher for storages without a bulk implementation (in-memory storage, custom
    ///     configurator storages). Creates the jobs one by one through the regular Hangfire client, so the
    ///     batch API keeps working — just without the single round trip guarantee.
    /// </summary>
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
                var state = entry.State;
                if (entry.ParentIndex.HasValue && state is AwaitingState awaiting)
                    state = new AwaitingState(ids[entry.ParentIndex.Value - 1], awaiting.NextState, awaiting.Options);
                ids[entry.Index - 1] = _backgroundJobClient.Create(entry.Job, state);
            }

            return Task.FromResult<IReadOnlyList<string>>(ids);
        }
    }
}
