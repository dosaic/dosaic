using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class QueryableExtensions
    {

        public static async Task ProcessAsync<T>(this IAsyncEnumerable<T> query, Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default)
        {
            await foreach (var entry in query.WithCancellation(cancellationToken))
                await function(entry, cancellationToken);
        }

        public static Task ProcessAsync<T>(this IQueryable<T> query, Func<T, CancellationToken, Task> function, CancellationToken cancellationToken = default) =>
            query.AsAsyncEnumerable().ProcessAsync(function, cancellationToken);
    }
}
