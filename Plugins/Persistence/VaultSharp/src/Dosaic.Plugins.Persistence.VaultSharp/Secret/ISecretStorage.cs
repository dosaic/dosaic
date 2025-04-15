namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

public interface ISecretStorage<TSecretBucket> where TSecretBucket : struct, Enum
{
    Task<Secret> GetSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default);
    Task<SecretId<TSecretBucket>> CreateSecretAsync(TSecretBucket bucket, Secret secret, CancellationToken cancellationToken = default);

    Task<SecretId<TSecretBucket>> UpdateSecretAsync(SecretId<TSecretBucket> secretId, Secret secret,
        CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default);
}
