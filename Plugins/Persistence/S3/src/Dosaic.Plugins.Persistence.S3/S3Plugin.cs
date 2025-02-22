using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.ApiEndpoints;

namespace Dosaic.Plugins.Persistence.S3
{
    public class S3Plugin : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
    {
        private readonly S3Configuration _configuration;

        public S3Plugin(S3Configuration configuration)
        {
            _configuration = configuration;
        }

        private IMinioClient GetMinioClient()
        {
            var minioClient = new MinioClient().WithEndpoint(_configuration.Endpoint);
            if (!string.IsNullOrEmpty(_configuration.AccessKey))
                minioClient = minioClient.WithCredentials(_configuration.AccessKey, _configuration.SecretKey);
            if (!string.IsNullOrEmpty(_configuration.Region))
                minioClient = minioClient.WithRegion(_configuration.Region);
            if (_configuration.UseSsl)
                minioClient = minioClient.WithSSL();
            return minioClient.Build();
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(GetMinioClient());
            serviceCollection.AddSingleton<IObjectOperations>(sp => sp.GetRequiredService<IMinioClient>());
            serviceCollection.AddSingleton<IBlobStorage, S3BlobStorage>();
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            var urlScheme = _configuration.UseSsl ? "https" : "http";
            var url = $"{urlScheme}://{_configuration.Endpoint}";
            if (!string.IsNullOrEmpty(_configuration.HealthCheckPath))
                url += $"/{_configuration.HealthCheckPath.TrimStart('/')}";
            healthChecksBuilder.AddUrlGroup(new Uri(url), "s3", HealthStatus.Unhealthy, new[] { HealthCheckTag.Readiness.Value });
        }
    }
}
