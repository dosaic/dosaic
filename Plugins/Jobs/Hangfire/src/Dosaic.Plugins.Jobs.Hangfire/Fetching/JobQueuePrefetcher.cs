using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     Fetches several queued jobs per round trip and hands them to the workers one by one.
    ///     Hangfire's stock PostgreSQL queue issues one <c>UPDATE ... LIMIT 1</c> per job, which becomes the
    ///     bottleneck once a queue is used at message bus volume.
    /// </summary>
    internal sealed class JobQueuePrefetcher : IDisposable
    {
        private readonly IJobQueueClient _client;
        private readonly PrefetchSettings _settings;
        private readonly Queue<PrefetchedQueueEntry> _buffer = new();

        /// <summary>Guards <see cref="_buffer" /> against the workers and the keep-alive timer.</summary>
        private readonly object _gate = new();

        /// <summary>
        ///     Buffered entries are already marked as fetched but not yet owned by a
        ///     <see cref="PrefetchedJob" />, so nothing else would renew their invisibility window.
        /// </summary>
        private readonly Timer _bufferKeepAliveTimer;

        private bool _disposed;

        public JobQueuePrefetcher(IJobQueueClient client, PrefetchSettings settings)
        {
            _client = client;
            _settings = settings;
            if (settings.SlidingKeepAliveInterval.HasValue && settings.PrefetchCount > 1)
                _bufferKeepAliveTimer = new Timer(_ => KeepBufferAlive(), null,
                    settings.SlidingKeepAliveInterval.Value, settings.SlidingKeepAliveInterval.Value);
        }

        public IFetchedJob Fetch(string[] queues, CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryDequeue(out var buffered))
                        return new PrefetchedJob(_client, buffered, _settings.SlidingKeepAliveInterval);
                    if (FillBuffer(queues)) continue;
                    cancellationToken.WaitHandle.WaitOne(_settings.PollInterval);
                }
            }
            catch (OperationCanceledException)
            {
                // the server is going down - hand everything we claimed but never dispatched back to the queue
                ReleaseBuffer();
                throw;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }

            StopTimer();
            ReleaseBuffer();
        }

        private bool TryDequeue(out PrefetchedQueueEntry entry)
        {
            lock (_gate) return _buffer.TryDequeue(out entry);
        }

        private bool FillBuffer(string[] queues)
        {
            lock (_gate)
            {
                if (_buffer.Count > 0) return true;
                if (_disposed) return false;
                var entries = _client.Fetch(queues, _settings.PrefetchCount, _settings.InvisibilityTimeout);
                foreach (var entry in entries) _buffer.Enqueue(entry);
                return entries.Count > 0;
            }
        }

        private void KeepBufferAlive()
        {
            lock (_gate)
            {
                if (_buffer.Count == 0) return;
                var buffered = _buffer.ToArray();
                var renewed = _client.KeepAlive(buffered);
                _buffer.Clear();
                foreach (var entry in buffered)
                {
                    // missing from the result means another server took the fetch over - forget the entry
                    if (!renewed.TryGetValue(entry.QueueEntryId, out var fetchedAt)) continue;
                    entry.FetchedAt = fetchedAt;
                    _buffer.Enqueue(entry);
                }
            }
        }

        private void ReleaseBuffer()
        {
            lock (_gate)
            {
                while (_buffer.TryDequeue(out var entry))
                    _client.Requeue(entry.QueueEntryId, entry.FetchedAt);
            }
        }

        private void StopTimer()
        {
            if (_bufferKeepAliveTimer is null) return;
            using var stopped = new ManualResetEvent(false);
            if (_bufferKeepAliveTimer.Dispose(stopped)) stopped.WaitOne();
        }
    }

    internal sealed class PrefetchSettings
    {
        public int PrefetchCount { get; init; } = 1;
        public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);
        public TimeSpan InvisibilityTimeout { get; init; } = TimeSpan.FromMinutes(30);
        public TimeSpan? SlidingKeepAliveInterval { get; init; }
    }
}
