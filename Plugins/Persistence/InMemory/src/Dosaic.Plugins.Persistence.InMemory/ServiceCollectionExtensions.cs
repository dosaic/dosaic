using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.InMemory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryRepository<TEntity, TId>(this IServiceCollection serviceCollection)
        where TEntity : class, IIdentifier<TId>

    {
        return serviceCollection.AddSingleton<InMemoryRepository<TEntity, TId>>();
    }
}
