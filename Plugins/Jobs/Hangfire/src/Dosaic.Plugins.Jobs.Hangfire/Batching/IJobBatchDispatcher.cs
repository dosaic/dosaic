namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Writes a prepared batch of jobs into the storage.
    /// </summary>
    public interface IJobBatchDispatcher
    {
        /// <summary>
        ///     Persists every entry and returns the created job ids, ordered by <see cref="BatchJobEntry.Index" />.
        /// </summary>
        Task<IReadOnlyList<string>> DispatchAsync(IReadOnlyList<BatchJobEntry> entries,
            CancellationToken cancellationToken = default);
    }
}
