using System.Runtime.CompilerServices;
using Hangfire;

namespace Dosaic.Plugins.Jobs.Hangfire.Uniqueness
{
    /// <summary>
    ///     Resolves the claim store for a storage. Job filter attributes are created by the runtime and
    ///     cannot take constructor dependencies, so the plugin registers the storage specific store here
    ///     while <see cref="StorageJobUniquenessStore" /> stays the default for everything else.
    /// </summary>
    internal static class JobUniquenessStores
    {
        private static readonly ConditionalWeakTable<JobStorage, IJobUniquenessStore> _stores = new();

        public static void Use(JobStorage storage, IJobUniquenessStore store) =>
            _stores.AddOrUpdate(storage, store);

        public static IJobUniquenessStore For(JobStorage storage) =>
            _stores.TryGetValue(storage, out var store) ? store : new StorageJobUniquenessStore(storage);
    }
}
