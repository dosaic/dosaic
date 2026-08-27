using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Batching
{
    public class BatchChunkerTests
    {
        private static BatchJobEntry Entry(int index, int? parent = null) =>
            new() { Index = index, ParentIndex = parent };

        [Test]
        public void ChunkSizeZeroKeepsEverythingInOneRoundTrip()
        {
            var entries = Enumerable.Range(1, 500).Select(x => Entry(x)).ToList();
            BatchChunker.Chunk(entries, 0).Should().ContainSingle().Which.Should().HaveCount(500);
        }

        [Test]
        public void ChunksAreCappedAtTheConfiguredSize()
        {
            var entries = Enumerable.Range(1, 10).Select(x => Entry(x)).ToList();
            var chunks = BatchChunker.Chunk(entries, 4).ToList();
            chunks.Should().HaveCount(3);
            chunks.Select(x => x.Count).Should().Equal(4, 4, 2);
            chunks.SelectMany(x => x).Select(x => x.Index).Should().Equal(Enumerable.Range(1, 10));
        }

        [Test]
        public void ContinuationChainsAreNeverSplitAcrossChunks()
        {
            List<BatchJobEntry> entries = [Entry(1), Entry(2, 1), Entry(3, 2), Entry(4), Entry(5)];
            var chunks = BatchChunker.Chunk(entries, 2).ToList();
            var chainChunk = chunks.Single(x => x.Any(e => e.Index == 1));
            chainChunk.Select(x => x.Index).Should().Equal(1, 2, 3);
        }
    }
}
