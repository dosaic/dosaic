using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeDetective;
using MimeDetective.Storage;

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

    public static IServiceCollection AddDefaultFileTypeDefinitionResolver(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AddSingleton<IFileTypeDefinitionResolver, DefaultFileTypeDefinitionResolver>();
    }

    public static IServiceCollection ReplaceDefaultFileTypeDefinitionResolver(this IServiceCollection serviceCollection,
        IFileTypeDefinitionResolver replacement)
    {
        return serviceCollection.Replace(ServiceDescriptor.Singleton(sp => replacement));
    }

    public static IServiceCollection ReplaceContentInspector(this IServiceCollection serviceCollection,
        IList<Definition> definitions)
    {
        return serviceCollection.Replace(ServiceDescriptor.Singleton(sp =>
            new ContentInspectorBuilder { Definitions = definitions }
                .Build()));
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
