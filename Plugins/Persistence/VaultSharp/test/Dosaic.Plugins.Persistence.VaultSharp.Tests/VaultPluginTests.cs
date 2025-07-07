using AwesomeAssertions;
using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using Dosaic.Testing.NUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NUnit.Framework;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.SecretsEngines.TOTP;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

public class VaultPluginTests
{
    private VaultConfiguration _vaultConfiguration;
    private VaultSharpPlugin _plugin;

    [SetUp]
    public void Up()
    {
        _vaultConfiguration = new VaultConfiguration { Token = "test-token", Url = "http://test.de:8100" };
        _plugin = new VaultSharpPlugin(_vaultConfiguration);
    }

    [Test]
    public void RegisterServices()
    {
        var sc = new ServiceCollection();
        _plugin.ConfigureServices(sc);
        sc.AddSingleton(new VaultConfiguration());
        sc.AddSecretStorage<SampleBucket>();
        var sp = sc.BuildServiceProvider();
        var vaultClient = sp.GetService<IVaultClient>()!;
        vaultClient.Should().NotBeNull();
        vaultClient.Settings.VaultServerUriWithPort.Should().Be("http://test.de:8100");
        vaultClient.Settings.SecretsEngineMountPoints.TOTP.Should().Be("totp");
        vaultClient.Settings.SecretsEngineMountPoints.KeyValueV2.Should().Be("credentials");
        var authMethodInfo = vaultClient.Settings.AuthMethodInfo.Should().BeOfType<TokenAuthMethodInfo>().Subject;
        authMethodInfo.VaultToken.Should().Be("test-token");

        sp.GetService<ITOTPSecretsEngine>().Should().NotBeNull();
        sp.GetService<IKeyValueSecretsEngineV2>().Should().NotBeNull();
        sp.GetService<ISecretStorage<SampleBucket>>().Should().NotBeNull();
    }

    [Test]
    public void RegisterHealthChecks()
    {
        var hc = Substitute.For<IHealthChecksBuilder>();
        _plugin.ConfigureHealthChecks(hc);
        var sc = TestingDefaults.ServiceCollection();
        sc.AddSingleton(Substitute.For<IVaultClient>());
        var sp = sc.BuildServiceProvider();
        hc.Received(1).Add(Arg.Is<HealthCheckRegistration>(h => h.Name == "vault"
                                                                && h.FailureStatus == HealthStatus.Unhealthy
                                                                && h.Factory(sp).GetType() == typeof(VaultHealthCheck)));
    }
}

public enum SampleBucket
{
    Certs = 0,
    ApiKeys = 1
}
