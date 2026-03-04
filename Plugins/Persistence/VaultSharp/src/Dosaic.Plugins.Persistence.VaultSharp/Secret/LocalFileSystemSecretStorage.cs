using System.Text.Json;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.VaultSharp.Types;

namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

public class LocalFileSystemSecretStorage<TSecretBucket>(VaultConfiguration configuration)
    : ISecretStorage<TSecretBucket>
    where TSecretBucket : struct, Enum
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    private string GetFilePath(SecretId<TSecretBucket> secretId)
    {
        var bucket = secretId.Bucket.ToString();
        var type = secretId.Type.ToString();
        var key = secretId.Key;
        return Path.Combine(configuration.LocalFileSystemPath, bucket, type, $"{key}.json");
    }

    public async Task<Secret> GetSecretAsync(SecretId<TSecretBucket> secretId,
        CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(secretId);
        if (!File.Exists(path))
            throw new NotFoundDosaicException(typeof(Secret), $"Secret not found: {secretId.Id}");

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return DeserializeSecret(secretId.Type, json);
    }

    public async Task<SecretId<TSecretBucket>> CreateSecretAsync(TSecretBucket bucket, Secret secret,
        CancellationToken cancellationToken = default)
    {
        ValidateTotp(secret);
        var newId = GetNewSecretId(bucket, secret);
        var path = GetFilePath(newId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = SerializeSecret(secret);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return newId;
    }

    public async Task<SecretId<TSecretBucket>> UpdateSecretAsync(SecretId<TSecretBucket> secretId, Secret secret,
        CancellationToken cancellationToken = default)
    {
        ValidateTotp(secret);
        var path = GetFilePath(secretId);
        if (!File.Exists(path))
            throw new NotFoundDosaicException(typeof(Secret), $"Secret not found: {secretId.Id}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = SerializeSecret(secret);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return secretId;
    }

    public Task DeleteSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(secretId);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private Secret DeserializeSecret(SecretType type, string json)
    {
        return type switch
        {
            SecretType.UsernamePassword =>
                JsonSerializer.Deserialize<UsernamePasswordSecret>(json, _jsonOptions)!,
            SecretType.UsernamePasswordApiKey =>
                JsonSerializer.Deserialize<UsernamePasswordApiKeySecret>(json, _jsonOptions)!,
            SecretType.UsernamePasswordTotp => DeserializeTotpSecret(json),
            SecretType.Certificate =>
                JsonSerializer.Deserialize<CertificateSecret>(json, _jsonOptions)!,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown secret type")
        };
    }

    private UsernamePasswordTotpSecret DeserializeTotpSecret(string json)
    {
        var stored = JsonSerializer.Deserialize<UsernamePasswordTotpSecret>(json, _jsonOptions)!;
        if (stored.Totp?.TotpKey is null)
            return stored;

        var (remaining, validUntil) = TotpCodeGenerator.GetPeriodInfo(configuration.TotpPeriodInSeconds);
        var code = TotpCodeGenerator.Generate(stored.Totp.TotpKey.Base32Key, configuration.TotpPeriodInSeconds);
        return stored with { Totp = new Totp(new TotpCode(code, validUntil, remaining), null) };
    }

    private static string SerializeSecret(Secret secret) =>
        JsonSerializer.Serialize(secret, secret.GetType(), _jsonOptions);

    private static SecretId<TSecretBucket> GetNewSecretId(TSecretBucket bucket, Secret secret)
    {
        var secretType = secret switch
        {
            CertificateSecret => SecretType.Certificate,
            UsernamePasswordApiKeySecret => SecretType.UsernamePasswordApiKey,
            UsernamePasswordTotpSecret => SecretType.UsernamePasswordTotp,
            UsernamePasswordSecret => SecretType.UsernamePassword,
            _ => throw new ArgumentOutOfRangeException(nameof(secret), secret, "Unknown secret type")
        };
        return SecretId<TSecretBucket>.New(bucket, secretType);
    }

    private static void ValidateTotp(Secret secret)
    {
        if (secret is not UsernamePasswordTotpSecret totpSecret) return;

        if (totpSecret.Totp?.TotpKey is null)
            throw new ValidationDosaicException(typeof(UsernamePasswordTotpSecret),
                "You need to provide the totp-key for writes");

        if (string.IsNullOrWhiteSpace(totpSecret.Totp.TotpKey.Base32Key))
            throw new ValidationDosaicException(typeof(UsernamePasswordTotpSecret),
                "The totp-key's Base32Key must not be null, empty, or whitespace");
    }
}
