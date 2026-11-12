using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Services;

namespace Dosaic.Plugins.Persistence.S3;

[Configuration("s3")]
public class S3Configuration
{
    public const string DefaultRegion = "us-east-1";

    public bool UseLocalFileSystem { get; set; }
    public string LocalFileSystemPath { get; set; } = "./nodep-s3";
    public string Endpoint { get; set; } = "";
    public string BucketPrefix { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "";
    public bool UseSsl { get; set; }
    /// <summary>
    /// Path appended to the endpoint for the anonymous URL readiness check. Not used when
    /// <see cref="S3HealthCheckConfiguration.Bucket"/> is set.
    /// </summary>
    public string HealthCheckPath { get; set; } = "";

    /// <summary>The readiness health check of this plugin.</summary>
    public S3HealthCheckConfiguration HealthCheck { get; set; } = new();

    /// <summary>Retries of the bucket migration (<c>AddFileStorageWithBucketMigration</c>).</summary>
    public S3BucketMigrationConfiguration BucketMigration { get; set; } = new();

    public bool SkipFileDeletion { get; set; }

    /// <summary>
    /// Use path style addressing (http://endpoint/bucket/key) instead of virtual hosted style
    /// (http://bucket.endpoint/key). Required by most S3 compatible servers like MinIO or Ceph.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Let the AWS SDK calculate and validate request/response checksums.
    /// Disabled by default because not every S3 compatible server supports them.
    /// </summary>
    public bool UseChecksums { get; set; }

    /// <summary>
    /// The service url the AWS S3 client connects to, built from <see cref="Endpoint"/> and <see cref="UseSsl"/>.
    /// If <see cref="Endpoint"/> already contains a scheme it is used as is.
    /// </summary>
    public string GetServiceUrl()
    {
        if (Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return Endpoint;
        return $"{(UseSsl ? "https" : "http")}://{Endpoint}";
    }

    /// <summary>
    /// The region used for request signing, falls back to <see cref="DefaultRegion"/> when not configured.
    /// </summary>
    public string GetSigningRegion() => string.IsNullOrEmpty(Region) ? DefaultRegion : Region;
}

public class S3HealthCheckConfiguration
{
    /// <summary>False registers no "s3" or "s3-local-filesystem" health check.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Bucket probed with a HEAD request that uses the configured credentials. The <c>bucketPrefix</c> is
    /// prepended, the same as for the bucket enums. Empty (default) keeps the anonymous URL check of the
    /// endpoint and <c>healthCheckPath</c>, which most S3 providers answer with 403.
    /// </summary>
    public string Bucket { get; set; } = "";
}

public class S3BucketMigrationConfiguration : StartupRetryOptions
{
    /// <summary>Defaults to 4 attempts, the count of earlier releases.</summary>
    public S3BucketMigrationConfiguration()
    {
        MaxAttempts = 4;
    }
}
