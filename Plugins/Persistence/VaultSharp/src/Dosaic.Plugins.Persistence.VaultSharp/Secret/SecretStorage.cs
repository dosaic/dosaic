using System.Globalization;
using System.Net;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.VaultSharp.Types;
using VaultSharp.Core;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using VaultSharp.V1.SecretsEngines.TOTP;

namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

public class SecretStorage<TSecretBucket>(
    VaultConfiguration configuration,
    IKeyValueSecretsEngineV2 keyValueSecretEngine,
    ITOTPSecretsEngine totpSecretEngine)
    : ISecretStorage<TSecretBucket>
    where TSecretBucket : struct, Enum
{
    public async Task<Secret> GetSecretAsync(SecretId<TSecretBucket> secretId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (secretId.Type)
            {
                case SecretType.UsernamePassword:
                    return (await keyValueSecretEngine.ReadSecretAsync<UsernamePasswordSecret>(secretId.Id)).Data.Data;
                case SecretType.UsernamePasswordApiKey:
                    return (await keyValueSecretEngine
                        .ReadSecretAsync<UsernamePasswordApiKeySecret>(secretId.Id)).Data.Data;
                case SecretType.UsernamePasswordTotp:
                    var upSecret =
                        (await keyValueSecretEngine.ReadSecretAsync<UsernamePasswordTotpSecret>(secretId.Id)).Data.Data;
                    var (remainingSeconds, validUntil) = GetTotpDuration();
                    var totpCode = (await totpSecretEngine.GetCodeAsync(secretId.Id)).Data.Code!;
                    return upSecret with
                    {
                        Totp = new Totp(new TotpCode(totpCode, validUntil, remainingSeconds), null)
                    };
                case SecretType.Certificate:
                    return (await keyValueSecretEngine.ReadSecretAsync<CertificateSecret>(secretId.Id)).Data.Data;
                default:
                    throw new ArgumentOutOfRangeException(nameof(secretId), "Invalid secret type");
            }
        }
        catch (VaultApiException vaultApiException)
        {
            if (vaultApiException.HttpStatusCode == HttpStatusCode.NotFound)
                throw new NotFoundDosaicException(typeof(Secret), "Could not find secret");
            throw new DosaicException("Unexpected exception during retrieval of secret", vaultApiException);
        }
    }

    public Task<SecretId<TSecretBucket>> CreateSecretAsync(TSecretBucket bucket, Secret secret,
        CancellationToken cancellationToken = default) =>
        WriteSecretAsync(GetNewSecretIdFromSecret(bucket, secret), secret);

    public Task<SecretId<TSecretBucket>> UpdateSecretAsync(SecretId<TSecretBucket> secretId, Secret secret,
        CancellationToken cancellationToken = default) => WriteSecretAsync(secretId, secret);

    public async Task DeleteSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default)
    {
        if (secretId.Type == SecretType.UsernamePasswordTotp)
            await totpSecretEngine.DeleteKeyAsync(secretId.Id);
        await keyValueSecretEngine.DeleteSecretAsync(secretId.Id);
    }

    private static SecretId<TSecretBucket> GetNewSecretIdFromSecret(TSecretBucket bucket, Secret secret)
    {
        var secretType = secret switch
        {
            CertificateSecret => SecretType.Certificate,
            UsernamePasswordApiKeySecret => SecretType.UsernamePasswordApiKey,
            UsernamePasswordTotpSecret => SecretType.UsernamePasswordTotp,
            UsernamePasswordSecret => SecretType.UsernamePassword,
            _ => throw new ArgumentOutOfRangeException(nameof(secret), secret, null)
        };
        var secretId = SecretId<TSecretBucket>.New(bucket, secretType);
        return secretId;
    }

    private async Task<SecretId<TSecretBucket>> WriteSecretAsync(SecretId<TSecretBucket> secretId, Secret secret)
    {
        try
        {
            if (secret is UsernamePasswordTotpSecret totpKeySecret)
            {
                if (totpKeySecret.Totp.TotpKey is null)
                    throw new ValidationDosaicException(typeof(UsernamePasswordTotpSecret),
                        "You need to provide the totp-key for writes");
                await totpSecretEngine.CreateKeyAsync(secretId.Id,
                    new TOTPCreateKeyRequest
                    {
                        KeyGenerationOption = new TOTPNonVaultBasedKeyGeneration
                        {
                            Key = totpKeySecret.Totp.TotpKey.Base32Key.Replace(" ", ""),
                            AccountName = secretId.Id,
                            Issuer = configuration.TotpIssuer
                        },
                        AccountName = secretId.Id,
                        Period = configuration.TotpPeriodInSeconds.ToString(CultureInfo.InvariantCulture),
                        Issuer = configuration.TotpIssuer
                    });
            }

            await keyValueSecretEngine.WriteSecretAsync(secretId.Id, secret);
            return secretId;
        }
        catch (VaultApiException vaultApiException)
        {
            throw new DosaicException("Unexpected exception during writing of secret", vaultApiException);
        }
    }

    private (int RemainingSeconds, DateTime ValidUntilUtc) GetTotpDuration()
    {
        var currentTime = DateTime.UtcNow;
        var elapsedTime = new DateTimeOffset(currentTime).ToUnixTimeSeconds() % configuration.TotpPeriodInSeconds;
        var timeRemaining = (int)(configuration.TotpPeriodInSeconds - elapsedTime);
        var validUntil = currentTime.AddSeconds(timeRemaining);
        validUntil = new DateTime(validUntil.Year, validUntil.Month, validUntil.Day, validUntil.Hour, validUntil.Minute,
            validUntil.Second);
        return (timeRemaining, validUntil);
    }
}
