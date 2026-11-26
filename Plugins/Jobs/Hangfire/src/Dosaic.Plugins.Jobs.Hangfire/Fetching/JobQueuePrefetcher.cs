using System.Diagnostics;
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

        private static readonly TimeSpan _minimumWait = TimeSpan.FromMilliseconds(1);

        /// <summary>
        ///     Guards <see cref="_buffer" /> and <see cref="_nextPollAt" /> against the workers and the keep-alive
        ///     timer. Waiting workers sleep on it with <see cref="Monitor.Wait(object, TimeSpan)" />.
        /// </summary>
        private readonly object _gate = new();

        /// <summary><see cref="Stopwatch" /> timestamp before which the queue is not asked again; 0 = ask now.</summary>
        private long _nextPollAt;

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

        /// <summary>
        ///     Hands out the next job. The workers of a queue share one poll schedule: after a round trip came
        ///     back empty, nobody asks the database again before <see cref="PrefetchSettings.PollInterval" /> has
        ///     passed, so an idle queue costs one query per interval and server instead of one per worker. A round
        ///     trip that found jobs reopens the schedule and wakes the waiting workers, so a backlog is drained
        ///     as fast as before.
        /// </summary>
        public IFetchedJob Fetch(string[] queues, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var wakeOnCancel = cancellationToken.Register(WakeWaitingWorkers);
                return new PrefetchedJob(_client, Next(queues, cancellationToken), _settings.SlidingKeepAliveInterval);
            }
            catch (OperationCanceledException)
            {
                // the server is going down - hand everything we claimed but never dispatched back to the queue
                ReleaseBuffer();
                throw;
            }
        }

        private PrefetchedQueueEntry Next(string[] queues, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_buffer.TryDequeue(out var buffered)) return buffered;
                    var now = Stopwatch.GetTimestamp();
                    if (!_disposed && now >= _nextPollAt)
                    {
                        if (FillBuffer(queues, now)) continue;
                        now = Stopwatch.GetTimestamp();
                    }

                    var wait = _disposed ? _settings.PollInterval : Stopwatch.GetElapsedTime(now, _nextPollAt);
                    Monitor.Wait(_gate, wait < _minimumWait ? _minimumWait : wait);
                }
            }
        }

        /// <summary>Runs under <see cref="_gate" />, so at most one round trip per prefetcher is in flight.</summary>
        private bool FillBuffer(string[] queues, long now)
        {
            IReadOnlyList<PrefetchedQueueEntry> entries;
            try
            {
                entries = _client.Fetch(queues, _settings.PrefetchCount, _settings.InvisibilityTimeout);
            }
            catch
            {
                // a failing database is not asked again by every waiting worker at once either
                _nextPollAt = now + PollIntervalTicks;
                throw;
            }

            if (entries.Count == 0)
            {
                _nextPollAt = now + PollIntervalTicks;
                return false;
            }

            foreach (var entry in entries) _buffer.Enqueue(entry);
            // the queue had work, so there may be more: let the next free worker ask right away
            _nextPollAt = 0;
            Monitor.PulseAll(_gate);
            return true;
        }

        private long PollIntervalTicks => (long)(_settings.PollInterval.TotalSeconds * Stopwatch.Frequency);

        private void WakeWaitingWorkers()
        {
            lock (_gate) Monitor.PulseAll(_gate);
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
