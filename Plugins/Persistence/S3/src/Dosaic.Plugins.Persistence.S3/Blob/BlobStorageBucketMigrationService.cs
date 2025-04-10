using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Dosaic.Plugins.Persistence.S3.Blob;

internal class BlobStorageBucketMigrationService<T>(IMinioClient minioClient, ILogger logger)
    : BackgroundService where T : struct, Enum
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var requiredBuckets = Enum.GetValues<T>()
                    .Select(x => x.GetName())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();

                var existingBuckets =
                    (await minioClient.ListBucketsAsync(stoppingToken)).Buckets.Select(x => x.Name).ToList();

                var missingBuckets = requiredBuckets.Except(existingBuckets).ToList();
                logger.LogInformation("S3 buckets {@Buckets}",
                    new
                    {
                        MissingBuckets = missingBuckets,
                        RequiredBuckets = requiredBuckets,
                        ExistingBuckets = existingBuckets
                    });

                if (missingBuckets.Count == 0) return;
                foreach (var missingBucket in missingBuckets)
                {
                    logger.LogInformation("S3 buckets: create missing bucket {missingBucket}", missingBucket);
                    await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(missingBucket), stoppingToken);
                }

                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not migrate database");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
