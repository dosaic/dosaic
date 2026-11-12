using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dosaic.Plugins.Persistence.S3;

/// <summary>
/// Sends a HEAD request for one bucket with the credentials of the S3 client, so it checks the endpoint, the
/// credentials and the bucket at once.
/// </summary>
internal class S3BucketHealthCheck(IAmazonS3 s3Client, string bucketName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.HeadBucketAsync(new HeadBucketRequest { BucketName = bucketName }, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                $"S3 bucket '{bucketName}' is not accessible.", ex);
        }
    }
}
