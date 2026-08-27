namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    /// <summary>Raw queue table access used by the prefetching fetch.</summary>
    internal interface IJobQueueClient
    {
        /// <summary>Marks up to <paramref name="count" /> queued jobs as fetched and returns them.</summary>
        IReadOnlyList<PrefetchedQueueEntry> Fetch(string[] queues, int count, TimeSpan invisibilityTimeout);

        /// <summary>Removes a successfully processed job from the queue.</summary>
        void Remove(long queueEntryId);

        /// <summary>Makes a job visible to other servers again.</summary>
        void Requeue(long queueEntryId);

        /// <summary>Extends the invisibility window of a job that is still being processed.</summary>
        void KeepAlive(long queueEntryId);
    }

    internal sealed record PrefetchedQueueEntry(long QueueEntryId, long JobId);
}
