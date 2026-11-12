using Amazon.S3;
using Amazon.S3.Model;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.S3.Blob;

internal class BlobStorageBucketMigrationService<T>(
    S3Configuration config,
    IAmazonS3 s3Client,
    ILogger logger,
    IFileStorage storage,
    IHostApplicationLifetime lifetime) : BackgroundService where T : struct, Enum
{
    internal Func<int, TimeSpan> RetryDelay { get; set; } = attempt => TimeSpan.FromSeconds(attempt);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.UseLocalFileSystem)
        {
            logger.LogInformation("S3 bucket migration skipped because local file system storage is used");
            return;
        }

        var bucketTypeName = typeof(T).Name;
        await StartupRetry.RunAsync(config.BucketMigration, $"migrate s3 buckets<{bucketTypeName}>",
            ct => MigrateAsync(bucketTypeName, ct), RetryDelay, logger, lifetime, stoppingToken);
    }

    private async Task MigrateAsync(string bucketTypeName, CancellationToken stoppingToken)
    {
        var requiredBuckets = Enum.GetValues<T>()
            .Where(x => !string.IsNullOrEmpty(x.GetName()))
            .Select(x => storage.ResolveBucketName(x.GetName()))
            .ToList();

        var existingBuckets =
            (await s3Client.ListBucketsAsync(stoppingToken)).Buckets?.Select(x => x.BucketName).ToList() ?? [];

        var missingBuckets = requiredBuckets.Except(existingBuckets).ToList();
        logger.LogInformation("S3 buckets<{bucketType} {@Buckets}", bucketTypeName,
            new
            {
                MissingBuckets = missingBuckets,
                RequiredBuckets = requiredBuckets,
                ExistingBuckets = existingBuckets
            });

        foreach (var missingBucket in missingBuckets)
        {
            logger.LogInformation("S3 buckets<{bucketType}>: create missing bucket {missingBucket}",
                bucketTypeName, missingBucket);
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = missingBucket }, stoppingToken);
        }
    }
}
