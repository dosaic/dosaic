using System.Collections.Concurrent;
using System.Globalization;
using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     Fetches several queued jobs per round trip and hands them to the workers one by one.
    ///     Hangfire's stock PostgreSQL queue issues one <c>UPDATE ... LIMIT 1</c> per job, which becomes the
    ///     bottleneck once a queue is used at message bus volume.
    /// </summary>
    internal sealed class JobQueuePrefetcher
    {
        private readonly IJobQueueClient _client;
        private readonly PrefetchSettings _settings;
        private readonly ConcurrentQueue<PrefetchedQueueEntry> _buffer = new();
        private readonly SemaphoreSlim _fetchLock = new(1, 1);

        public JobQueuePrefetcher(IJobQueueClient client, PrefetchSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        public IFetchedJob Fetch(string[] queues, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_buffer.TryDequeue(out var buffered))
                    return new PrefetchedJob(_client, buffered.QueueEntryId,
                        buffered.JobId.ToString(CultureInfo.InvariantCulture), _settings.SlidingKeepAliveInterval);
                if (FillBuffer(queues, cancellationToken)) continue;
                cancellationToken.WaitHandle.WaitOne(_settings.PollInterval);
            }
        }

        private bool FillBuffer(string[] queues, CancellationToken cancellationToken)
        {
            _fetchLock.Wait(cancellationToken);
            try
            {
                if (!_buffer.IsEmpty) return true;
                var entries = _client.Fetch(queues, _settings.PrefetchCount, _settings.InvisibilityTimeout);
                foreach (var entry in entries) _buffer.Enqueue(entry);
                return entries.Count > 0;
            }
            finally
            {
                _fetchLock.Release();
            }
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
