using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit.Assertions;
using FluentAssertions;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Result;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests;

public class BlobStorageBucketMigrationServiceTests
{
    [Test]
    public async Task ShouldMigrateBlobStorageBucket()
    {
        var mc = Substitute.For<IMinioClient>();

        mc.ListBucketsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new Exception("retry"),
                _ => new ListAllMyBucketsResult { Buckets = [new Bucket { Name = "NOT_REQUIRED_BUCKET" }] });

        var buckets = Enum.GetValues<SampleBucket>().Select(x => x.GetName()).ToList();
        var svc = new BlobStorageBucketMigrationService<SampleBucket>(mc, new FakeLogger<BlobStorageBucketMigrationService<SampleBucket>>());
        await svc.StartAsync(CancellationToken.None);
        svc.ExecuteTask.Should().NotBeNull();
        await svc.ExecuteTask!;
        await svc.StopAsync(CancellationToken.None);
        await mc.Received(2).ListBucketsAsync(Arg.Any<CancellationToken>());
        await mc.Received(buckets.Count).MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());
    }
}
