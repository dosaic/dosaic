using System.Globalization;
using Dosaic.Extensions.Sqids;
using Dosaic.Plugins.Persistence.VaultSharp.Secret;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.VaultSharp.Tests;

public class SecretIdTests
{
    [Test]
    public void CanBeTranslatedToSqid()
    {
        var secretId = new SecretId<SampleBucket>(SampleBucket.Certs, SecretType.Certificate, "my-certificate");
        var stringId = secretId.Id;
        var newSecretId = SecretId<SampleBucket>.FromSqid(stringId);
        newSecretId.Should().Be(secretId);
    }

    [Test]
    public void CanGenerateNewIds()
    {
        var id = SecretId<SampleBucket>.New(SampleBucket.Certs, SecretType.UsernamePassword);
        id.Type.Should().Be(SecretType.UsernamePassword);
        id.Bucket.Should().Be(SampleBucket.Certs);
        id.Key.Should().NotBeEmpty();
        Guid.TryParse(id.Key, out _).Should().BeTrue();
        var sqid = id.Id.FromSqid();
        sqid.Should().NotBeNullOrEmpty();
        var parts = sqid.Split(':');
        parts.Should().HaveCount(3);
        parts[0].Should().Be(SampleBucket.Certs.ToString());
        parts[1].Should().Be(((int)SecretType.UsernamePassword).ToString(CultureInfo.InvariantCulture));
        parts[2].Should().Be(id.Key);
    }

    [Test]
    public void TryParseReturnsTrueForValidFileId()
    {
        SecretId<SampleBucket>.TryParse("wL7v8CUcUIjFPeALpGM0xYQydYdFpsVZoMnGg9a1BOvFyuNlY1yAU2FvGBRdes", out var fileId).Should().BeTrue();
        var stringId = fileId.Id;
        var newSecretId = SecretId<SampleBucket>.FromSqid(stringId);
        newSecretId.Should().Be(fileId);
        newSecretId.Bucket.Should().Be(SampleBucket.Certs);
        newSecretId.Key.Should().Be("my-certificate");
        newSecretId.Type.Should().Be(SecretType.Certificate);
    }

    [Test]
    public void TryParseReturnsFalseForInvalidFileId()
    {
        SecretId<SampleBucket>.TryParse("something:key", out var fileId).Should().BeFalse();
        var stringId = fileId.Id;
        var newSecretId = SecretId<SampleBucket>.FromSqid(stringId);
        newSecretId.Should().NotBe(fileId);
    }
}
