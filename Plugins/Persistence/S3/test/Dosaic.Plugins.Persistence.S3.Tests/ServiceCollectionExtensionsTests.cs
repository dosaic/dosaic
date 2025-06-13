using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeDetective;
using MimeDetective.Storage;
using Minio;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests;

public class ServiceCollectionExtensionsTests
{
    private IServiceCollection _serviceCollection;

    [SetUp]
    public void Setup()
    {
        _serviceCollection = TestingDefaults.ServiceCollection();
    }

    [Test]
    public void AddS3BlobStoragePluginWithoutConfigurationRegistersPlugin()
    {
        _serviceCollection.AddS3BlobStoragePlugin(new S3Configuration()
        {
            Endpoint = "test-endpoint",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
        });

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IMinioClient) &&
            x.ImplementationInstance.GetType() == typeof(MinioClient));

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IContentInspector) &&
            x.ImplementationInstance.GetType().Name == "ContentInspectorImpl");
    }

    [Test]
    public void AddFileStorageRegistersFileStorage()
    {
        _serviceCollection.AddFileStorage<SampleBucket>();

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IFileStorage<SampleBucket>) &&
            x.ImplementationType == typeof(FileStorage<SampleBucket>));
    }

    [Test]
    public void AddBlobStorageBucketMigrationServiceRegistersMigrationService()
    {
        _serviceCollection.AddBlobStorageBucketMigrationService<SampleBucket>();

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IHostedService) &&
            x.ImplementationType == typeof(BlobStorageBucketMigrationService<SampleBucket>));
    }

    [Test]
    public void AddFileStorageBucketMigrationServiceRegistersMigrationService()
    {
        _serviceCollection.AddFileStorageWithBucketMigration<SampleBucket>();

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IHostedService) &&
            x.ImplementationType == typeof(BlobStorageBucketMigrationService<SampleBucket>));
    }

    [Test]
    public void ServiceReplacementWorks()
    {
        _serviceCollection.AddS3BlobStoragePlugin(new S3Configuration()
        {
            Endpoint = "test-endpoint",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
        });

        _serviceCollection.ReplaceContentInspector(new List<Definition>());
        _serviceCollection.ReplaceDefaultFileTypeDefinitionResolver<EmptyFileTypeDefinitionResolver>();

        var sp = _serviceCollection.BuildServiceProvider();

        sp.GetRequiredService<IFileTypeDefinitionResolver>().Should().BeOfType<EmptyFileTypeDefinitionResolver>();
        var contentInspector = sp.GetRequiredService<IContentInspector>();
        var matchers = contentInspector.GetInaccessibleValue("DefinitionMatchers");
        matchers.GetType().GetProperty("Length")!.GetValue(matchers).Should().Be(0);
    }
}
