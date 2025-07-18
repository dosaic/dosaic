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

        public static async Task<IList<T>> ProcessAndGetAsync<T>(this IAsyncEnumerable<T> query,
            Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default)
        {
            var result = new List<T>();
            await foreach (var entry in query.WithCancellation(cancellationToken))
            {
                await function(entry, cancellationToken);
                result.Add(entry);
            }
            return result;
        }

        public static Task<IList<T>> ProcessAndGetAsync<T>(this IQueryable<T> query,
            Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default) =>
            query.AsAsyncEnumerable().ProcessAndGetAsync(function, cancellationToken);

    }
}
