using Microsoft.Extensions.Diagnostics.HealthChecks;
using SystemIO = System.IO;

namespace Dosaic.Plugins.Persistence.S3;

internal class LocalFileSystemStorageHealthCheck(string path) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".health-probe");
            SystemIO.File.WriteAllText(probe, "ok");
            SystemIO.File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Local filesystem storage path '{path}' is not accessible.", ex));
        }
    }
}
