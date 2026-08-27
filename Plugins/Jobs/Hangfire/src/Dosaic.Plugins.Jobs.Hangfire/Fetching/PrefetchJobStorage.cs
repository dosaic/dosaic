using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     Wraps the PostgreSQL job storage and replaces the one-job-per-query fetch with a batched
    ///     prefetching fetch that is configurable per queue.
    /// </summary>
    internal sealed class PrefetchJobStorage : JobStorage
    {
        private readonly JobStorage _inner;
        private readonly IJobQueueClient _client;
        private readonly IReadOnlyDictionary<string, PrefetchSettings> _settingsPerQueue;
        private readonly PrefetchSettings _defaultSettings;
        private readonly ConcurrentDictionary<string, JobQueuePrefetcher> _prefetchers = new();

        public PrefetchJobStorage(JobStorage inner, IJobQueueClient client,
            IReadOnlyDictionary<string, PrefetchSettings> settingsPerQueue, PrefetchSettings defaultSettings)
        {
            _inner = inner;
            _client = client;
            _settingsPerQueue = settingsPerQueue;
            _defaultSettings = defaultSettings;
            JobExpirationTimeout = inner.JobExpirationTimeout;
        }

        public override IMonitoringApi GetMonitoringApi() => _inner.GetMonitoringApi();

        public override IStorageConnection GetConnection()
        {
            var connection = _inner.GetConnection();
            return connection is JobStorageConnection storageConnection
                ? new PrefetchStorageConnection(storageConnection, Fetch)
                : connection;
        }

        public override IStorageConnection GetReadOnlyConnection() => _inner.GetReadOnlyConnection();
        [Obsolete("Overridden to stay in sync with the wrapped storage, which still implements it.")]
        public override IEnumerable<IServerComponent> GetComponents() => _inner.GetComponents();
        public override IEnumerable<IBackgroundProcess> GetServerRequiredProcesses() => _inner.GetServerRequiredProcesses();
        public override IEnumerable<IBackgroundProcess> GetStorageWideProcesses() => _inner.GetStorageWideProcesses();
        public override IEnumerable<IStateHandler> GetStateHandlers() => _inner.GetStateHandlers();
        public override void WriteOptionsToLog(ILog logger) => _inner.WriteOptionsToLog(logger);
        public override bool HasFeature(string featureId) => _inner.HasFeature(featureId);
        public override string ToString() => _inner.ToString();

        private IFetchedJob Fetch(string[] queues, CancellationToken cancellationToken)
        {
            var key = string.Join(",", queues);
            var prefetcher = _prefetchers.GetOrAdd(key,
                _ => new JobQueuePrefetcher(_client, ResolveSettings(queues)));
            return prefetcher.Fetch(queues, cancellationToken);
        }

        private PrefetchSettings ResolveSettings(string[] queues)
        {
            var matches = queues.Where(_settingsPerQueue.ContainsKey).Select(x => _settingsPerQueue[x]).ToList();
            if (matches.Count == 0) return _defaultSettings;
            return new PrefetchSettings
            {
                PrefetchCount = matches.Max(x => x.PrefetchCount),
                PollInterval = matches.Min(x => x.PollInterval),
                InvisibilityTimeout = _defaultSettings.InvisibilityTimeout,
                SlidingKeepAliveInterval = _defaultSettings.SlidingKeepAliveInterval
            };
        }
    }
}
