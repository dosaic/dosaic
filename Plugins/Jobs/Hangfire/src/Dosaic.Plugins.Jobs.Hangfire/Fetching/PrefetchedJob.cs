using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     A job that was pulled out of the queue by <see cref="JobQueuePrefetcher" />.
    /// </summary>
    internal sealed class PrefetchedJob : IFetchedJob
    {
        private readonly IJobQueueClient _client;
        private readonly long _queueEntryId;
        private readonly Timer _slidingTimer;
        private bool _completed;

        public PrefetchedJob(IJobQueueClient client, long queueEntryId, string jobId, TimeSpan? slidingInterval)
        {
            _client = client;
            _queueEntryId = queueEntryId;
            JobId = jobId;
            if (slidingInterval.HasValue)
                _slidingTimer = new Timer(_ => KeepAlive(), null, slidingInterval.Value, slidingInterval.Value);
        }

        public string JobId { get; }

        public void RemoveFromQueue()
        {
            if (_completed) return;
            _completed = true;
            _client.Remove(_queueEntryId);
        }

        public void Requeue()
        {
            if (_completed) return;
            _completed = true;
            _client.Requeue(_queueEntryId);
        }

        public void Dispose()
        {
            _slidingTimer?.Dispose();
            Requeue();
        }

        private void KeepAlive()
        {
            if (_completed) return;
            _client.KeepAlive(_queueEntryId);
        }
    }
}
