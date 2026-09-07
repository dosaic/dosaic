using Amazon.Runtime;
using Amazon.S3;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.S3.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MimeDetective;

namespace Dosaic.Plugins.Persistence.S3;

public class S3FileStoragePlugin(S3Configuration configuration)
    : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        if (configuration.UseLocalFileSystem)
        {
            serviceCollection.AddSingleton<IFileStorage>(
                new LocalFileSystemBlobStorage(configuration.LocalFileSystemPath, configuration.SkipFileDeletion));
        }
        else
        {
            serviceCollection.AddSingleton(GetS3Client());
            serviceCollection.AddFileStorage();
        }

        serviceCollection.AddDefaultFileTypeDefinitionResolver();
        serviceCollection.AddSingleton<IContentInspector>(
            new ContentInspectorBuilder { Definitions = MimeDetective.Definitions.DefaultDefinitions.All() }
                .Build());
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        if (configuration.UseLocalFileSystem)
        {
            var localPath = configuration.LocalFileSystemPath;
            healthChecksBuilder.Add(new HealthCheckRegistration(
                "s3-local-filesystem",
                _ => new LocalFileSystemStorageHealthCheck(localPath),
                HealthStatus.Unhealthy,
                [HealthCheckTag.Readiness.Value]));
        }
        else
        {
            var url = configuration.GetServiceUrl();
            if (!string.IsNullOrEmpty(configuration.HealthCheckPath))
                url += $"/{configuration.HealthCheckPath.TrimStart('/')}";
            healthChecksBuilder.AddUrlGroup(new Uri(url), "s3", HealthStatus.Unhealthy,
                [HealthCheckTag.Readiness.Value]);
        }
    }

    internal IAmazonS3 GetS3Client()
    {
        var s3Config = new AmazonS3Config
        {
            ServiceURL = configuration.GetServiceUrl(),
            AuthenticationRegion = configuration.GetSigningRegion(),
            ForcePathStyle = configuration.ForcePathStyle,
            RequestChecksumCalculation = configuration.UseChecksums
                ? RequestChecksumCalculation.WHEN_SUPPORTED
                : RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = configuration.UseChecksums
                ? ResponseChecksumValidation.WHEN_SUPPORTED
                : ResponseChecksumValidation.WHEN_REQUIRED
        };
        var credentials = GetCredentials();
        // without explicit credentials the AWS SDK default credential chain is used
        // (environment variables, shared config file, EC2/ECS/EKS instance metadata, ...)
        return credentials is null ? new AmazonS3Client(s3Config) : new AmazonS3Client(credentials, s3Config);
    }

    internal AWSCredentials GetCredentials() =>
        string.IsNullOrEmpty(configuration.AccessKey)
            ? null
            : new BasicAWSCredentials(configuration.AccessKey, configuration.SecretKey);
}
