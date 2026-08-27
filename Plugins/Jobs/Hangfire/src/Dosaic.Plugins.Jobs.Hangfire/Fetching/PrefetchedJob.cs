using System.Globalization;
using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     A job that was pulled out of the queue by <see cref="JobQueuePrefetcher" />.
    /// </summary>
    internal sealed class PrefetchedJob : IFetchedJob
    {
        private readonly IJobQueueClient _client;
        private readonly PrefetchedQueueEntry _entry;
        private readonly Timer _slidingTimer;

        /// <summary>
        ///     Guards <see cref="_completed" /> and the fetch timestamp against the keep-alive timer thread,
        ///     the same way Hangfire's own <c>PostgreSqlFetchedJob</c> does.
        /// </summary>
        private readonly object _gate = new();

        private bool _completed;

        public PrefetchedJob(IJobQueueClient client, PrefetchedQueueEntry entry, TimeSpan? slidingInterval)
        {
            _client = client;
            _entry = entry;
            JobId = entry.JobId.ToString(CultureInfo.InvariantCulture);
            if (slidingInterval.HasValue)
                _slidingTimer = new Timer(_ => KeepAlive(), null, slidingInterval.Value, slidingInterval.Value);
        }

        public string JobId { get; }

        public void RemoveFromQueue()
        {
            lock (_gate)
            {
                if (_completed) return;
                _completed = true;
                _client.Remove(_entry.QueueEntryId, _entry.FetchedAt);
            }
        }

        public void Requeue()
        {
            lock (_gate)
            {
                if (_completed) return;
                _completed = true;
                _client.Requeue(_entry.QueueEntryId, _entry.FetchedAt);
            }
        }

        public void Dispose()
        {
            // must not hold the gate here — a running keep-alive callback needs it to finish
            StopTimer();
            Requeue();
        }

        private void StopTimer()
        {
            if (_slidingTimer is null) return;
            using var stopped = new ManualResetEvent(false);
            if (_slidingTimer.Dispose(stopped)) stopped.WaitOne();
        }

        private void KeepAlive()
        {
            lock (_gate)
            {
                if (_completed) return;
                var renewed = _client.KeepAlive(_entry.QueueEntryId, _entry.FetchedAt);
                // no row matched — another server took the fetch over, so this job is no longer ours to release
                if (renewed is null) _completed = true;
                else _entry.FetchedAt = renewed.Value;
            }
        }
    }
}
