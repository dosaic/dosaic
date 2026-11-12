using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.Blob;

[NonParallelizable]
public class BlobStorageBucketMigrationServiceTests
{
    private IAmazonS3 _s3Client = null!;
    private FakeLogger<BlobStorageBucketMigrationService<SampleBucket>> _logger = null!;
    private IFileStorage _fileStorage = null!;
    private IHostApplicationLifetime _lifetime = null!;

    [SetUp]
    public void Init()
    {
        _s3Client = Substitute.For<IAmazonS3>();
        _logger = new FakeLogger<BlobStorageBucketMigrationService<SampleBucket>>();
        _fileStorage = Substitute.For<IFileStorage>();
        _fileStorage.ResolveBucketName(Arg.Any<string>()).Returns(info => $"dev-{info.Args()[0]}");
        _lifetime = Substitute.For<IHostApplicationLifetime>();
        Environment.ExitCode = 0;
    }

    [TearDown]
    public void Cleanup()
    {
        Environment.ExitCode = 0;
    }

    private async Task RunAsync(S3Configuration configuration)
    {
        var svc = new BlobStorageBucketMigrationService<SampleBucket>(configuration, _s3Client, _logger,
            _fileStorage, _lifetime)
        { RetryDelay = _ => TimeSpan.Zero };
        await svc.StartAsync(CancellationToken.None);
        svc.ExecuteTask.Should().NotBeNull();
        await svc.ExecuteTask!;
        await svc.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ShouldMigrateBlobStorageBucket()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new Exception("retry"),
                _ => new ListBucketsResponse
                {
                    Buckets =
                    [
                        new S3Bucket { BucketName = "NOT_REQUIRED_BUCKET" },
                        new S3Bucket { BucketName = "logos" }
                    ]
                });

        await RunAsync(new S3Configuration());

        await _s3Client.Received(2).ListBucketsAsync(Arg.Any<CancellationToken>());
        await _s3Client.Received(1)
            .PutBucketAsync(Arg.Is<PutBucketRequest>(r => r.BucketName == "dev-test-logos"),
                Arg.Any<CancellationToken>());
        await _s3Client.Received(1)
            .PutBucketAsync(Arg.Is<PutBucketRequest>(r => r.BucketName == "dev-test.docs"),
                Arg.Any<CancellationToken>());

        _logger.Entries[0].Message.Should()
            .Be("Could not migrate s3 buckets<SampleBucket> (attempt 1 of 4) -> retrying");

        // fakelogger does not resolve nested lists and objects but serilog does
        _logger.Entries[1].Message.Should().Be(
            "S3 buckets<SampleBucket { MissingBuckets = System.Collections.Generic.List`1[System.String], RequiredBuckets = System.Collections.Generic.List`1[System.String], ExistingBuckets = System.Collections.Generic.List`1[System.String] }");
        _logger.Entries[2].Message.Should().Be("S3 buckets<SampleBucket>: create missing bucket dev-test-logos");
        _logger.Entries[3].Message.Should().Be("S3 buckets<SampleBucket>: create missing bucket dev-test.docs");
    }

    [Test]
    public async Task ShouldNotCreateBucketsThatAlreadyExist()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>()).Returns(new ListBucketsResponse
        {
            Buckets = [new S3Bucket { BucketName = "dev-test-logos" }, new S3Bucket { BucketName = "dev-test.docs" }]
        });

        await RunAsync(new S3Configuration());

        await _s3Client.DidNotReceive().PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>());
        _logger.Entries.Should().ContainSingle();
    }

    [Test]
    public async Task ShouldMakeFourAttemptsByDefaultAndSayHowMany()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new Exception("retry"));

        await RunAsync(new S3Configuration());

        await _s3Client.Received(4).ListBucketsAsync(Arg.Any<CancellationToken>());
        _logger.Entries.Select(x => x.Message).Should().Equal(
            "Could not migrate s3 buckets<SampleBucket> (attempt 1 of 4) -> retrying",
            "Could not migrate s3 buckets<SampleBucket> (attempt 2 of 4) -> retrying",
            "Could not migrate s3 buckets<SampleBucket> (attempt 3 of 4) -> retrying",
            "Could not migrate s3 buckets<SampleBucket> after 4 attempts -> giving up");
        _lifetime.DidNotReceive().StopApplication();
        Environment.ExitCode.Should().Be(0);
    }

    [Test]
    public async Task ShouldMakeTheConfiguredNumberOfAttempts()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new Exception("retry"));

        await RunAsync(new S3Configuration { BucketMigration = { MaxAttempts = 2 } });

        await _s3Client.Received(2).ListBucketsAsync(Arg.Any<CancellationToken>());
        _logger.Entries.Last().Message.Should()
            .Be("Could not migrate s3 buckets<SampleBucket> after 2 attempts -> giving up");
    }

    [Test]
    public async Task ShouldStopTheHostWhenTheBucketsCannotBeCreatedAndStopHostOnFailureIsSet()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new Exception("retry"));

        await RunAsync(new S3Configuration { BucketMigration = { MaxAttempts = 1, StopHostOnFailure = true } });

        await _s3Client.Received(1).ListBucketsAsync(Arg.Any<CancellationToken>());
        _logger.Entries.Single().Message.Should()
            .Be("Could not migrate s3 buckets<SampleBucket> after 1 attempts -> stopping the host");
        _lifetime.Received(1).StopApplication();
        Environment.ExitCode.Should().Be(1);
    }

    [Test]
    public async Task ShouldNotMigrateBucketsWhenUsingLocalFileSystem()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new Exception("retry"));

        await RunAsync(new S3Configuration { UseLocalFileSystem = true });

        await _s3Client.Received(0).ListBucketsAsync(Arg.Any<CancellationToken>());
        _logger.Entries[0].Message.Should().Be("S3 bucket migration skipped because local file system storage is used");
    }
}
