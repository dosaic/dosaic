using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines;

namespace Dosaic.Plugins.Persistence.VaultSharp;

public class VaultSharpPlugin(VaultConfiguration configuration)
    : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        var vaultSettings = new VaultClientSettings(configuration.Url, new TokenAuthMethodInfo(configuration.Token))
        {
            SecretsEngineMountPoints = new SecretsEngineMountPoints { TOTP = "totp", KeyValueV2 = "credentials" }
        };
        serviceCollection.AddSingleton<IVaultClient>(new VaultClient(vaultSettings));
        serviceCollection.AddSingleton(sp => sp.GetRequiredService<IVaultClient>().V1.Secrets.KeyValue.V2);
        serviceCollection.AddSingleton(sp => sp.GetRequiredService<IVaultClient>().V1.Secrets.TOTP);
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        healthChecksBuilder.AddCheck<VaultHealthCheck>("vault", HealthStatus.Unhealthy,
            [HealthCheckTag.Readiness.Value]);
    }
}
