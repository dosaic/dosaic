using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dosaic.Plugins.Persistence.VaultSharp;

internal class LocalFileSystemVaultHealthCheck(string path) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".health-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Local filesystem vault path '{path}' is not accessible.", ex));
        }
    }
}
