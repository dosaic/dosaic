using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MimeDetective;
using Minio;

namespace Dosaic.Plugins.Persistence.S3;

public class S3FileStoragePlugin(S3Configuration configuration)
    : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(GetMinioClient());
        serviceCollection.AddDefaultFileTypeDefinitionResolver();
        serviceCollection.AddFileStorage();
        serviceCollection.AddSingleton<IContentInspector>(
            new ContentInspectorBuilder { Definitions = MimeDetective.Definitions.DefaultDefinitions.All() }
                .Build());
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        var urlScheme = configuration.UseSsl ? "https" : "http";
        var url = $"{urlScheme}://{configuration.Endpoint}";
        if (!string.IsNullOrEmpty(configuration.HealthCheckPath))
            url += $"/{configuration.HealthCheckPath.TrimStart('/')}";
        healthChecksBuilder.AddUrlGroup(new Uri(url), "s3", HealthStatus.Unhealthy,
            [HealthCheckTag.Readiness.Value]);
    }

    private IMinioClient GetMinioClient()
    {
        var minioClient = new MinioClient().WithEndpoint(configuration.Endpoint);
        if (!string.IsNullOrEmpty(configuration.AccessKey))
            minioClient = minioClient.WithCredentials(configuration.AccessKey, configuration.SecretKey);
        if (!string.IsNullOrEmpty(configuration.Region))
            minioClient = minioClient.WithRegion(configuration.Region);
        if (configuration.UseSsl)
            minioClient = minioClient.WithSSL();
        return minioClient.Build();
    }
}
