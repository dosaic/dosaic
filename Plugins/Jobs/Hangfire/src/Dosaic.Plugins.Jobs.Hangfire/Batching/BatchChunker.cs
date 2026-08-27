namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Splits a batch into chunks without ever separating a continuation chain, because the
    ///     antecedent job id is only known inside the statement that creates it.
    /// </summary>
    internal static class BatchChunker
    {
        public static IEnumerable<IReadOnlyList<BatchJobEntry>> Chunk(IReadOnlyList<BatchJobEntry> entries,
            int chunkSize)
        {
            if (chunkSize <= 0 || entries.Count <= chunkSize)
            {
                yield return entries;
                yield break;
            }

            var byIndex = entries.ToDictionary(x => x.Index);
            var groups = new Dictionary<int, List<BatchJobEntry>>();
            var order = new List<int>();
            foreach (var entry in entries)
            {
                var root = entry;
                while (root.ParentIndex.HasValue && byIndex.TryGetValue(root.ParentIndex.Value, out var parent))
                    root = parent;
                if (!groups.TryGetValue(root.Index, out var group))
                {
                    group = [];
                    groups[root.Index] = group;
                    order.Add(root.Index);
                }

                group.Add(entry);
            }

            var current = new List<BatchJobEntry>();
            foreach (var group in order.Select(x => groups[x]))
            {
                if (current.Count > 0 && current.Count + group.Count > chunkSize)
                {
                    yield return current;
                    current = [];
                }

                current.AddRange(group);
            }

            if (current.Count > 0) yield return current;
        }
    }
}
