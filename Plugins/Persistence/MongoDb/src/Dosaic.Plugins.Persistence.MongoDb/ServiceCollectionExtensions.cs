using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.MongoDb;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDbRepository<TEntity, TId>(this IServiceCollection serviceCollection)
        where TEntity : class, IIdentifier<TId>

    {
        return serviceCollection.AddSingleton<MongoRepository<TEntity, TId>>();
    }
}
