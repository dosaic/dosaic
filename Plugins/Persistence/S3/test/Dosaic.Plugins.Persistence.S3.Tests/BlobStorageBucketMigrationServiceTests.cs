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
                _ => new ListAllMyBucketsResult { Buckets = [new Bucket { Name = "dev-NOT_REQUIRED_BUCKET" }] });

        var buckets = Enum.GetValues<SampleBucket>().Select(x => x.GetName()).ToList();
        var fakeLogger = new FakeLogger<BlobStorageBucketMigrationService<SampleBucket>>();
        var fileStorage = Substitute.For<IFileStorage>();
        fileStorage.ResolveBucketName(Arg.Any<string>()).Returns(info => $"dev-{info.Args()[0]}");
        var svc = new BlobStorageBucketMigrationService<SampleBucket>(mc, fakeLogger, fileStorage);
        await svc.StartAsync(CancellationToken.None);
        svc.ExecuteTask.Should().NotBeNull();
        await svc.ExecuteTask!;
        await svc.StopAsync(CancellationToken.None);
        await mc.Received(2).ListBucketsAsync(Arg.Any<CancellationToken>());
        await mc.Received(buckets.Count).MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());

        fakeLogger.Entries[0].Message.Should().Be("Could not migrate s3 buckets<SampleBucket> -> retrying");

        // fakelogger does not resolve nested lists and objects but serilog does
        fakeLogger.Entries[1].Message.Should().Be(
            "S3 buckets<SampleBucket { MissingBuckets = System.Collections.Generic.List`1[System.String], RequiredBuckets = System.Collections.Generic.List`1[System.String], ExistingBuckets = System.Collections.Generic.List`1[System.String] }");
        fakeLogger.Entries[2].Message.Should().Be("S3 buckets<SampleBucket>: create missing bucket dev-logos");
        fakeLogger.Entries[3].Message.Should().Be("S3 buckets<SampleBucket>: create missing bucket dev-docs");
    }
}
