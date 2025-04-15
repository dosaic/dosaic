using System.Net;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using Dosaic.Plugins.Persistence.VaultSharp.Types;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using VaultSharp.Core;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.SecretsEngines.TOTP;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

public class SecretStorageTests
{
    private IKeyValueSecretsEngineV2 _keyValueSecretEngine;
    private ITOTPSecretsEngine _totpSecretEngine;
    private ISecretStorage<SampleBucket> _secretStorage;
    private VaultConfiguration _config;
    private const SampleBucket Bucket = SampleBucket.Certs;

    [SetUp]
    public void Up()
    {
        _totpSecretEngine = Substitute.For<ITOTPSecretsEngine>();
        _keyValueSecretEngine = Substitute.For<IKeyValueSecretsEngineV2>();
        _config = new VaultConfiguration();
        _secretStorage = new SecretStorage<SampleBucket>(_config, _keyValueSecretEngine, _totpSecretEngine);
    }

    private static SecretId<SampleBucket> GetId(SecretType secretType) =>
        new(Bucket, secretType, Guid.NewGuid().ToString("N"));

    private static Secret<SecretData<TSecret>> GetSecret<TSecret>(TSecret secret) where TSecret : Secret.Secret
    {
        return new Secret<SecretData<TSecret>> { Data = new SecretData<TSecret> { Data = secret } };
    }

    [Test]
    public async Task ShouldThrowNotFoundWhenSecretIsNotFound()
    {
        _keyValueSecretEngine.ReadSecretAsync<UsernamePasswordSecret>(Arg.Any<string>())
            .Throws(new VaultApiException(HttpStatusCode.NotFound, "test"));
        var dosaicException = (await _secretStorage.Invoking(async x => await x.GetSecretAsync(GetId(SecretType.UsernamePassword)))
            .Should().ThrowAsync<NotFoundDosaicException>()).Which;
        dosaicException.Should().BeAssignableTo<NotFoundDosaicException>();
    }

    [Test]
    public async Task ShouldThrowUnhandledWhenSomethingUnwantedHappens()
    {
        _keyValueSecretEngine.ReadSecretAsync<UsernamePasswordSecret>(Arg.Any<string>())
            .Throws(new VaultApiException(HttpStatusCode.BadRequest, "test"));
        var dosaicException = (await _secretStorage.Invoking(async x => await x.GetSecretAsync(GetId(SecretType.UsernamePassword)))
            .Should().ThrowAsync<DosaicException>()).Which;

        dosaicException.Should().BeAssignableTo<DosaicException>();

        _keyValueSecretEngine.WriteSecretAsync(Arg.Any<string>(), Arg.Any<UsernamePasswordSecret>(), Arg.Any<int?>(), Arg.Any<string>())
            .Throws(new VaultApiException(HttpStatusCode.BadRequest, "test"));
        dosaicException = (await _secretStorage.Invoking(async x => await x.CreateSecretAsync(Bucket, new UsernamePasswordSecret("test", "test")))
            .Should().ThrowAsync<DosaicException>()).Which;
        dosaicException.Should().BeAssignableTo<DosaicException>();
    }

    [Test]
    public async Task ShouldThrowOnInvalidSecretType()
    {
        const SecretType SecType = (SecretType)1000;
        await _secretStorage.Invoking(async x => await x.GetSecretAsync(new SecretId<SampleBucket>(Bucket, SecType, "test")))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CanGetUsernamePasswordSecret()
    {
        var secretId = GetId(SecretType.UsernamePassword);
        var upSecret = new UsernamePasswordSecret("test", "test");
        var secret = GetSecret(upSecret);
        _keyValueSecretEngine.ReadSecretAsync<UsernamePasswordSecret>(secretId.Id)
            .Returns(secret);

        var result = await _secretStorage.GetSecretAsync(secretId);
        result.Should().Be(upSecret);
    }

    [Test]
    public async Task CanGetUsernamePasswordApiKeySecret()
    {
        var secretId = GetId(SecretType.UsernamePasswordApiKey);
        var upaSecret = new UsernamePasswordApiKeySecret("test", "test", "123");
        var secret = GetSecret(upaSecret);
        _keyValueSecretEngine.ReadSecretAsync<UsernamePasswordApiKeySecret>(secretId.Id)
            .Returns(secret);

        var result = await _secretStorage.GetSecretAsync(secretId);
        result.Should().Be(upaSecret);
    }

    [Test]
    public async Task CanGetUsernamePasswordTotpSecret()
    {
        var secretId = GetId(SecretType.UsernamePasswordTotp);
        var uptSecret = new UsernamePasswordTotpSecret("test", "test", new Totp(null, null));
        var secret = GetSecret(uptSecret);
        _keyValueSecretEngine.ReadSecretAsync<UsernamePasswordTotpSecret>(secretId.Id)
            .Returns(secret);
        _totpSecretEngine.GetCodeAsync(secretId.Id, Arg.Any<string>(), Arg.Any<string>())
            .Returns(new Secret<TOTPCode> { Data = new TOTPCode { Code = "123456" } });

        var now = DateTime.UtcNow;
        var validUntil = now.AddSeconds(now.Second >= 30 ? 60 - now.Second : 30 - now.Second);
        var seconds = (int)(validUntil - now).TotalSeconds;
        var result = (UsernamePasswordTotpSecret)await _secretStorage.GetSecretAsync(secretId);
        result.Username.Should().Be("test");
        result.Password.Should().Be("test");
        result.Totp.Should().NotBeNull();
        result.Totp.TotpKey.Should().BeNull();
        result.Totp.TotpCode.Should().NotBeNull();
        result.Totp.TotpCode!.Code.Should().Be("123456");
        result.Totp.TotpCode!.ValidTillUtc.Should().BeCloseTo(validUntil, TimeSpan.FromSeconds(1));
        result.Totp.TotpCode!.RemainingSeconds.Should().BeCloseTo(seconds, 1);
    }

    [Test]
    public async Task CanGetCertificateSecret()
    {
        var secretId = GetId(SecretType.Certificate);
        var certSecret = new CertificateSecret("test", "test");
        var secret = GetSecret(certSecret);
        _keyValueSecretEngine.ReadSecretAsync<CertificateSecret>(secretId.Id)
            .Returns(secret);

        var result = await _secretStorage.GetSecretAsync(secretId);
        result.Should().Be(certSecret);
    }

    [Test]
    public async Task CantWriteUsernamePasswordTotpSecretWhenNoKeyIsPresent()
    {
        var uptSecret = new UsernamePasswordTotpSecret("test", "test", new Totp(null, null));
        var error = (await _secretStorage.Invoking(async x => await x.CreateSecretAsync(Bucket, uptSecret))
            .Should().ThrowAsync<ValidationDosaicException>()).Which;
        error.Should().BeAssignableTo<ValidationDosaicException>();
        error.Message.Should().Contain("totp-key");

    }

    [Test]
    public async Task CanWriteUsernamePasswordTotpSecret()
    {
        _config = new VaultConfiguration() { TotpIssuer = "TestIssuer", TotpPeriodInSeconds = 60 };
        _secretStorage = new SecretStorage<SampleBucket>(_config, _keyValueSecretEngine, _totpSecretEngine);

        var uptSecret = new UsernamePasswordTotpSecret("test", "test", new Totp(null, new TotpKey("123")));
        var id = await _secretStorage.CreateSecretAsync(Bucket, uptSecret);
        await _secretStorage.UpdateSecretAsync(id, uptSecret);
        await _totpSecretEngine.Received(2).CreateKeyAsync(
            id.Id,
            Arg.Is<TOTPCreateKeyRequest>(t =>
                t.KeyGenerationOption.GetType() == typeof(TOTPNonVaultBasedKeyGeneration)
                && ((TOTPNonVaultBasedKeyGeneration)t.KeyGenerationOption).Key == "123"
                && ((TOTPNonVaultBasedKeyGeneration)t.KeyGenerationOption).AccountName == id.Id
                && ((TOTPNonVaultBasedKeyGeneration)t.KeyGenerationOption).Issuer == "TestIssuer"
                && t.Period == "60"
                && t.Issuer == "TestIssuer"
                && t.AccountName == id.Id
                )
            );
        await _keyValueSecretEngine.Received(2).WriteSecretAsync(id.Id, uptSecret);
    }

    [Test]
    public async Task CanWriteUsernamePasswordApiKeySecret()
    {
        var upaSecret = new UsernamePasswordApiKeySecret("test", "test", "123");
        var id = await _secretStorage.CreateSecretAsync(Bucket, upaSecret);
        await _secretStorage.UpdateSecretAsync(id, upaSecret);
        await _keyValueSecretEngine.Received(2).WriteSecretAsync(id.Id, upaSecret);
    }

    [Test]
    public async Task CanWriteUsernamePasswordSecret()
    {
        var secret = new UsernamePasswordSecret("test", "test");
        var id = await _secretStorage.CreateSecretAsync(Bucket, secret);
        await _secretStorage.UpdateSecretAsync(id, secret);
        await _keyValueSecretEngine.Received(2).WriteSecretAsync(id.Id, secret);
    }

    [Test]
    public async Task CanWriteCertificateSecret()
    {
        var secret = new CertificateSecret("test", "test");
        var id = await _secretStorage.CreateSecretAsync(Bucket, secret);
        await _secretStorage.UpdateSecretAsync(id, secret);
        await _keyValueSecretEngine.Received(2).WriteSecretAsync(id.Id, secret);
    }

    [Test]
    public async Task CanDeleteSecrets()
    {
        var secretId = SecretId<SampleBucket>.New(Bucket, SecretType.UsernamePassword);
        await _secretStorage.DeleteSecretAsync(secretId);
        await _keyValueSecretEngine.Received(1).DeleteSecretAsync(secretId.Id);
    }

    [Test]
    public async Task CanDeleteSecretsWithTotp()
    {
        var secretId = SecretId<SampleBucket>.New(Bucket, SecretType.UsernamePasswordTotp);
        await _secretStorage.DeleteSecretAsync(secretId);
        await _keyValueSecretEngine.Received(1).DeleteSecretAsync(secretId.Id);
        await _totpSecretEngine.Received(1).DeleteKeyAsync(secretId.Id);
    }

    [Test]
    public async Task CantCreateNewSecretsWhichAreNotImplemented()
    {
        var secret = new TestSecret();
        await _secretStorage.Invoking(async x => await x.CreateSecretAsync(Bucket, secret))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
    public record TestSecret : Secret.Secret;
}
