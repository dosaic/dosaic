using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using VaultSharp;

namespace Dosaic.Plugins.Persistence.VaultSharp;

internal class VaultHealthCheck(IVaultClient vault, ILogger<VaultHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var result = await vault.V1.System.GetHealthStatusAsync();
            return result?.HttpStatusCode == 200
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Vault health check failed with status code {result?.HttpStatusCode}");
        }
        catch (Exception e)
        {
            const string FailureMessage = "Failure checking vault health";
            logger.LogError(e, FailureMessage);
            return HealthCheckResult.Unhealthy(FailureMessage);
        }
    }
}
