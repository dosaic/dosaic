using AwesomeAssertions;
using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using Dosaic.Testing.NUnit;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using VaultSharp;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.SecretsEngines.TOTP;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

public class ServiceCollectionExtensionsTests
{
    private IServiceCollection _serviceCollection;

    [SetUp]
    public void Setup()
    {
        _serviceCollection = TestingDefaults.ServiceCollection();
    }

    [Test]
    public void AddVaultSharpPluginRegistersServices()
    {
        _serviceCollection.AddVaultSharpPlugin(new VaultConfiguration()
        {
            Token = "asdf",
            Url = "http://localhost"
        });

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IVaultClient) &&
            x.Lifetime == ServiceLifetime.Singleton &&
            x.ImplementationInstance.GetType() == typeof(VaultClient));

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(IKeyValueSecretsEngineV2) &&
            x.Lifetime == ServiceLifetime.Singleton &&
            x.ImplementationFactory != null);

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(ITOTPSecretsEngine) &&
            x.Lifetime == ServiceLifetime.Singleton &&
            x.ImplementationFactory != null);
    }

    [Test]
    public void AddFileStorageRegistersFileStorage()
    {
        _serviceCollection.AddSecretStorage<SampleBucket>();

        _serviceCollection.Should().Contain(x =>
            x.ServiceType == typeof(ISecretStorage<SampleBucket>) &&
            x.Lifetime == ServiceLifetime.Singleton &&
            x.ImplementationType == typeof(SecretStorage<SampleBucket>));
    }
}
