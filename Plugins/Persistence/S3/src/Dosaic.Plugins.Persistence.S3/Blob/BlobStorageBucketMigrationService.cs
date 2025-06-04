using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Dosaic.Plugins.Persistence.S3.Blob;

internal class BlobStorageBucketMigrationService<T>(IMinioClient minioClient, ILogger logger, IFileStorage storage)
    : BackgroundService where T : struct, Enum
{
    private int _retryCount = 1;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var bucketTypeName = typeof(T).Name;
            try
            {
                var requiredBuckets = Enum.GetValues<T>()
                    .Where(x => !string.IsNullOrEmpty(x.GetName()))
                    .Select(x => storage.ResolveBucketName(x.GetName()))
                    .ToList();

                var existingBuckets =
                    (await minioClient.ListBucketsAsync(stoppingToken)).Buckets.Select(x => storage.ResolveBucketName(x.Name)).ToList();

                var missingBuckets = requiredBuckets.Except(existingBuckets).ToList();
                logger.LogInformation("S3 buckets<{bucketType} {@Buckets}", bucketTypeName,
                    new
                    {
                        MissingBuckets = missingBuckets,
                        RequiredBuckets = requiredBuckets,
                        ExistingBuckets = existingBuckets
                    });

                if (missingBuckets.Count == 0) return;
                foreach (var missingBucket in missingBuckets)
                {
                    logger.LogInformation("S3 buckets<{bucketType}>: create missing bucket {missingBucket}",
                        bucketTypeName, missingBucket);
                    await minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(storage.ResolveBucketName(missingBucket)), stoppingToken);
                }

                return;
            }
            catch (Exception e)
            {
                if (_retryCount >= 4)
                {
                    logger.LogError(e, "Could not migrate s3 buckets<{bucketType}> after 3 attempts -> giving up", bucketTypeName);
                    return;
                }
                logger.LogError(e, "Could not migrate s3 buckets<{bucketType}> -> retrying", bucketTypeName);
                await Task.Delay(_retryCount * 1000, stoppingToken);
                _retryCount++;
            }
        }
    }
}
