using Hangfire.Server;
using Hangfire.Storage;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>
    ///     Delegates everything to the wrapped storage connection except <see cref="FetchNextJob" />,
    ///     which is served from a prefetch buffer.
    /// </summary>
    internal sealed class PrefetchStorageConnection : JobStorageConnection
    {
        private readonly JobStorageConnection _inner;
        private readonly Func<string[], CancellationToken, IFetchedJob> _fetch;

        public PrefetchStorageConnection(JobStorageConnection inner,
            Func<string[], CancellationToken, IFetchedJob> fetch)
        {
            _inner = inner;
            _fetch = fetch;
        }

        public override IFetchedJob FetchNextJob(string[] queues, CancellationToken cancellationToken) =>
            _fetch(queues, cancellationToken);

        public override void Dispose() => _inner.Dispose();
        public override IWriteOnlyTransaction CreateWriteTransaction() => _inner.CreateWriteTransaction();
        public override IDisposable AcquireDistributedLock(string resource, TimeSpan timeout) => _inner.AcquireDistributedLock(resource, timeout);
        public override string CreateExpiredJob(global::Hangfire.Common.Job job, IDictionary<string, string> parameters, DateTime createdAt, TimeSpan expireIn) => _inner.CreateExpiredJob(job, parameters, createdAt, expireIn);
        public override void SetJobParameter(string id, string name, string value) => _inner.SetJobParameter(id, name, value);
        public override string GetJobParameter(string id, string name) => _inner.GetJobParameter(id, name);
        public override JobData GetJobData(string jobId) => _inner.GetJobData(jobId);
        public override StateData GetStateData(string jobId) => _inner.GetStateData(jobId);
        public override void AnnounceServer(string serverId, ServerContext context) => _inner.AnnounceServer(serverId, context);
        public override void RemoveServer(string serverId) => _inner.RemoveServer(serverId);
        public override void Heartbeat(string serverId) => _inner.Heartbeat(serverId);
        public override int RemoveTimedOutServers(TimeSpan timeOut) => _inner.RemoveTimedOutServers(timeOut);
        public override HashSet<string> GetAllItemsFromSet(string key) => _inner.GetAllItemsFromSet(key);
        public override string GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore) => _inner.GetFirstByLowestScoreFromSet(key, fromScore, toScore);
        public override List<string> GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore, int count) => _inner.GetFirstByLowestScoreFromSet(key, fromScore, toScore, count);
        public override void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs) => _inner.SetRangeInHash(key, keyValuePairs);
        public override Dictionary<string, string> GetAllEntriesFromHash(string key) => _inner.GetAllEntriesFromHash(key);
        public override long GetSetCount(string key) => _inner.GetSetCount(key);
        public override long GetSetCount(IEnumerable<string> keys, int limit) => _inner.GetSetCount(keys, limit);
        public override bool GetSetContains(string key, string value) => _inner.GetSetContains(key, value);
        public override List<string> GetRangeFromSet(string key, int startingFrom, int endingAt) => _inner.GetRangeFromSet(key, startingFrom, endingAt);
        public override TimeSpan GetSetTtl(string key) => _inner.GetSetTtl(key);
        public override string GetValueFromHash(string key, string name) => _inner.GetValueFromHash(key, name);
        public override long GetHashCount(string key) => _inner.GetHashCount(key);
        public override TimeSpan GetHashTtl(string key) => _inner.GetHashTtl(key);
        public override long GetListCount(string key) => _inner.GetListCount(key);
        public override List<string> GetAllItemsFromList(string key) => _inner.GetAllItemsFromList(key);
        public override List<string> GetRangeFromList(string key, int startingFrom, int endingAt) => _inner.GetRangeFromList(key, startingFrom, endingAt);
        public override TimeSpan GetListTtl(string key) => _inner.GetListTtl(key);
        public override long GetCounter(string key) => _inner.GetCounter(key);
        public override DateTime GetUtcDateTime() => _inner.GetUtcDateTime();
    }
}
