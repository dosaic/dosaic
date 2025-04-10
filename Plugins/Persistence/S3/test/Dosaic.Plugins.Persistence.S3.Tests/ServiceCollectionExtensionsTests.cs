using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeDetective;
using Minio;
using NSubstitute;
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
            x.ImplementationFactory != null);
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
        _serviceCollection.AddBlobStorageBucketMigrationService(SampleBucket.Logos);

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IHostedService) &&
            x.ImplementationType == typeof(BlobStorageBucketMigrationService<SampleBucket>));
    }
}
