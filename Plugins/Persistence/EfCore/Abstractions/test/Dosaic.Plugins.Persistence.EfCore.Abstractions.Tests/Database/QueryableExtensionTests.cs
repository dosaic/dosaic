using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using EntityFrameworkCore.Testing.Common;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    public class QueryableExtensionTests
    {
        [Test]
        public async Task ProcessAsyncWillProcessAllItemsAndReturnsProcessedCount()
        {
            var processedItems = new List<int>();
            var itemValues = new List<int> { 1, 2, 3, 4, 5 };
            var items = new AsyncEnumerable<int>(itemValues).AsQueryable();

            var processedCount = await items.ProcessAsync(async (item, ct) =>
            {
                await Task.Delay(1, ct);
                processedItems.Add(item);
            });

            processedCount.Should().Be(itemValues.Count);
            processedItems.Should().BeEquivalentTo(itemValues);
        }
    }
}
