using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using Dosaic.Plugins.Persistence.VaultSharp.Types;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

[TestFixture]
public class LocalFileSystemSecretStorageTests
{
    private string _tempPath;
    private VaultConfiguration _config;
    private ISecretStorage<SampleBucket> _storage;

    [SetUp]
    public void SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _config = new VaultConfiguration
        {
            UseLocalFileSystem = true,
            LocalFileSystemPath = _tempPath,
            TotpPeriodInSeconds = 30,
            TotpIssuer = "TestIssuer"
        };
        _storage = new LocalFileSystemSecretStorage<SampleBucket>(_config);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
    }

    [Test]
    public async Task CreateAndGetUsernamePasswordSecret()
    {
        var secret = new UsernamePasswordSecret("user1", "pass1");
        var id = await _storage.CreateSecretAsync(SampleBucket.Certs, secret);
        id.Type.Should().Be(SecretType.UsernamePassword);

        var retrieved = await _storage.GetSecretAsync(id);
        var up = retrieved.Should().BeOfType<UsernamePasswordSecret>().Subject;
        up.Username.Should().Be("user1");
        up.Password.Should().Be("pass1");
    }

    [Test]
    public async Task CreateAndGetUsernamePasswordApiKeySecret()
    {
        var secret = new UsernamePasswordApiKeySecret("user2", "pass2", "apikey123");
        var id = await _storage.CreateSecretAsync(SampleBucket.ApiKeys, secret);
        id.Type.Should().Be(SecretType.UsernamePasswordApiKey);

        var retrieved = await _storage.GetSecretAsync(id);
        var upak = retrieved.Should().BeOfType<UsernamePasswordApiKeySecret>().Subject;
        upak.Username.Should().Be("user2");
        upak.Password.Should().Be("pass2");
        upak.ApiKey.Should().Be("apikey123");
    }

    [Test]
    public async Task CreateAndGetCertificateSecret()
    {
        var secret = new CertificateSecret("cert-content", "passphrase");
        var id = await _storage.CreateSecretAsync(SampleBucket.Certs, secret);
        id.Type.Should().Be(SecretType.Certificate);

        var retrieved = await _storage.GetSecretAsync(id);
        var cert = retrieved.Should().BeOfType<CertificateSecret>().Subject;
        cert.Certificate.Should().Be("cert-content");
        cert.Passphrase.Should().Be("passphrase");
    }

    [Test]
    public async Task CreateAndGetTotpSecretGeneratesCode()
    {
        // JBSWY3DPEHPK3PXP is a well-known test Base32 key
        var secret = new UsernamePasswordTotpSecret("totpuser", "totppass",
            new Totp(null, new TotpKey("JBSWY3DPEHPK3PXP")));
        var id = await _storage.CreateSecretAsync(SampleBucket.Certs, secret);
        id.Type.Should().Be(SecretType.UsernamePasswordTotp);

        var retrieved = await _storage.GetSecretAsync(id);
        var totp = retrieved.Should().BeOfType<UsernamePasswordTotpSecret>().Subject;
        totp.Username.Should().Be("totpuser");
        totp.Password.Should().Be("totppass");
        totp.Totp.Should().NotBeNull();
        totp.Totp.TotpCode.Should().NotBeNull();
        totp.Totp.TotpCode.Code.Should().HaveLength(6);
        totp.Totp.TotpCode.RemainingSeconds.Should().BeInRange(1, 30);
        totp.Totp.TotpKey.Should().BeNull("TotpKey is stripped from responses");
    }

    [Test]
    public async Task UpdateSecret()
    {
        var secret = new UsernamePasswordSecret("original", "original-pass");
        var id = await _storage.CreateSecretAsync(SampleBucket.Certs, secret);

        var updated = new UsernamePasswordSecret("updated", "updated-pass");
        await _storage.UpdateSecretAsync(id, updated);

        var retrieved = await _storage.GetSecretAsync(id);
        var up = retrieved.Should().BeOfType<UsernamePasswordSecret>().Subject;
        up.Username.Should().Be("updated");
        up.Password.Should().Be("updated-pass");
    }

    [Test]
    public async Task DeleteSecret()
    {
        var secret = new UsernamePasswordSecret("user", "pass");
        var id = await _storage.CreateSecretAsync(SampleBucket.Certs, secret);

        await _storage.DeleteSecretAsync(id);

        var act = async () => await _storage.GetSecretAsync(id);
        await act.Should().ThrowAsync<NotFoundDosaicException>();
    }

    [Test]
    public async Task GetMissingSecretThrowsNotFound()
    {
        var id = SecretId<SampleBucket>.New(SampleBucket.Certs, SecretType.UsernamePassword);
        var act = async () => await _storage.GetSecretAsync(id);
        await act.Should().ThrowAsync<NotFoundDosaicException>();
    }

    [Test]
    public async Task CreateTotpSecretWithoutKeyThrows()
    {
        var secret = new UsernamePasswordTotpSecret("u", "p", new Totp(null, null));
        var act = async () => await _storage.CreateSecretAsync(SampleBucket.Certs, secret);
        await act.Should().ThrowAsync<ValidationDosaicException>();
    }
}

[TestFixture]
public class TotpCodeGeneratorTests
{
    [Test]
    public void GenerateProducesValidSixDigitCode()
    {
        // JBSWY3DPEHPK3PXP is a well-known test Base32 key for "Hello!"
        const string testKey = "JBSWY3DPEHPK3PXP";
        var code = TotpCodeGenerator.Generate(testKey, 30);
        code.Should().HaveLength(6);
        int.Parse(code, System.Globalization.CultureInfo.InvariantCulture).Should().BeInRange(0, 999999);
    }

    [Test]
    public void GetPeriodInfoReturnsValidRange()
    {
        var (remaining, validUntil) = TotpCodeGenerator.GetPeriodInfo(30);
        remaining.Should().BeInRange(1, 30);
        validUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Test]
    public void ConsecutiveCallsReturnSameCodeWithinPeriod()
    {
        const string testKey = "JBSWY3DPEHPK3PXP";
        var code1 = TotpCodeGenerator.Generate(testKey, 30);
        var code2 = TotpCodeGenerator.Generate(testKey, 30);
        code1.Should().Be(code2);
    }
}
