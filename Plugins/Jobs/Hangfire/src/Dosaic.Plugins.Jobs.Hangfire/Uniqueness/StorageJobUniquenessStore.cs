using Hangfire;

namespace Dosaic.Plugins.Jobs.Hangfire.Uniqueness
{
    /// <summary>
    ///     Fallback for storages without a bulk claim statement (in-memory storage, storages brought by an
    ///     <see cref="IHangfireConfigurator" />). Serializes the check behind a distributed lock instead of
    ///     relying on a unique index.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="PostgresJobUniquenessStore" /> this cannot take over an expired claim, because
    ///     Hangfire's storage API cannot read the score of a single set entry. A claim that is never
    ///     released therefore blocks its fingerprint until the set entry is removed.
    /// </remarks>
    internal sealed class StorageJobUniquenessStore : IJobUniquenessStore
    {
        private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
        private readonly JobStorage _storage;

        public StorageJobUniquenessStore(JobStorage storage) => _storage = storage;

        public IReadOnlyCollection<JobUniquenessClaim> Claim(IReadOnlyList<JobUniquenessClaim> claims, double now)
        {
            if (claims.Count == 0) return [];
            var owned = new List<JobUniquenessClaim>();
            using var connection = _storage.GetConnection();
            foreach (var perSet in claims.GroupBy(x => x.SetKey))
            {
                using var distributedLock = connection.AcquireDistributedLock($"{perSet.Key}:lock", _lockTimeout);
                // copied, because the returned set belongs to the storage and must not be mutated
                var taken = new HashSet<string>(connection.GetAllItemsFromSet(perSet.Key) ?? []);
                using var transaction = connection.CreateWriteTransaction();
                var claimed = false;
                foreach (var claim in perSet)
                {
                    if (!taken.Add(claim.Fingerprint)) continue;
                    transaction.AddToSet(perSet.Key, claim.Fingerprint, claim.ExpiresAt);
                    owned.Add(claim);
                    claimed = true;
                }

                if (claimed) transaction.Commit();
            }

            return owned;
        }
    }
}
