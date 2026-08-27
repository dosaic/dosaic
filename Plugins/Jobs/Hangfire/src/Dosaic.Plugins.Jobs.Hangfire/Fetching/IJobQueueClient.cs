namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>Raw queue table access used by the prefetching fetch.</summary>
    internal interface IJobQueueClient
    {
        /// <summary>Marks up to <paramref name="count" /> queued jobs as fetched and returns them.</summary>
        IReadOnlyList<PrefetchedQueueEntry> Fetch(string[] queues, int count, TimeSpan invisibilityTimeout);

        /// <summary>
        ///     Removes a successfully processed job from the queue, but only while we still own the fetch —
        ///     see <see cref="PrefetchedQueueEntry.FetchedAt" />.
        /// </summary>
        void Remove(long queueEntryId, DateTime fetchedAt);

        /// <summary>Makes a job visible to other servers again, but only while we still own the fetch.</summary>
        void Requeue(long queueEntryId, DateTime fetchedAt);

        /// <summary>
        ///     Extends the invisibility window of a job that is still being processed and returns the new
        ///     fetch timestamp, or null when the fetch has been taken over by another server.
        /// </summary>
        DateTime? KeepAlive(long queueEntryId, DateTime fetchedAt);

        /// <summary>
        ///     Extends the invisibility window of several still buffered jobs in a single round trip and
        ///     returns the new fetch timestamp per queue entry. Entries that were taken over by another
        ///     server are missing from the result.
        /// </summary>
        IReadOnlyDictionary<long, DateTime> KeepAlive(IReadOnlyList<PrefetchedQueueEntry> entries);
    }

    /// <summary>
    ///     A queue row this server has claimed. Every mutation is scoped to <see cref="FetchedAt" /> the same
    ///     way Hangfire's own <c>PostgreSqlFetchedJob</c> does it, so a row another server re-fetched after
    ///     the invisibility window elapsed is never touched again by us.
    /// </summary>
    internal sealed class PrefetchedQueueEntry
    {
        public PrefetchedQueueEntry(long queueEntryId, long jobId, DateTime fetchedAt)
        {
            QueueEntryId = queueEntryId;
            JobId = jobId;
            FetchedAt = fetchedAt;
        }

        public long QueueEntryId { get; }
        public long JobId { get; }

        /// <summary>The fetch timestamp we own; updated by every successful keep-alive.</summary>
        public DateTime FetchedAt { get; set; }
    }
}
