using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.S3;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3BlobStoragePlugin(this IServiceCollection serviceCollection,
        S3Configuration configuration)
    {
        new S3FileStoragePlugin(configuration).ConfigureServices(serviceCollection);
        return serviceCollection;
    }

    public static IServiceCollection AddFileStorage<T>(this IServiceCollection serviceCollection)
        where T : struct, Enum
    {
        return serviceCollection.AddSingleton<IFileStorage<T>, FileStorage<T>>();
    }

    public static IServiceCollection AddFileStorage(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddSingleton<IFileStorage, FileStorage>();
    }

    public static IServiceCollection AddFileStorageWithBucketMigration<T>(this IServiceCollection serviceCollection)
        where T : struct, Enum
    {
        return serviceCollection.AddSingleton<IFileStorage<T>, FileStorage<T>>()
            .AddBlobStorageBucketMigrationService<T>();
    }

    public static IServiceCollection AddBlobStorageBucketMigrationService<T>(this IServiceCollection serviceCollection)
        where T : struct, Enum
    {
        return serviceCollection.AddHostedService<BlobStorageBucketMigrationService<T>>();
    }
}
