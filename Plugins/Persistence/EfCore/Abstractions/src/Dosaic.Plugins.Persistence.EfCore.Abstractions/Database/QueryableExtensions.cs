using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class QueryableExtensions
    {

        public static async Task<int> ProcessAsync<T>(this IAsyncEnumerable<T> query, Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var entry in query.WithCancellation(cancellationToken))
            {
                await function(entry, cancellationToken);
                count++;
            }

            return count;
        }

        public static Task<int> ProcessAsync<T>(this IQueryable<T> query, Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default) =>
            query.AsAsyncEnumerable().ProcessAsync(function, cancellationToken);
    }
}
