using System.Globalization;
using Dosaic.Extensions.Sqids;

namespace Dosaic.Plugins.Persistence.VaultSharp.Secret;

public readonly struct SecretId<TSecretBucket>(TSecretBucket bucket, SecretType type, string key) where TSecretBucket : struct, Enum
{
    public TSecretBucket Bucket { get; } = bucket;
    public SecretType Type { get; } = type;
    public string Key { get; } = key;
    public string Id => $"{Bucket}:{(int)Type}:{Key}".ToSqid();

    public static SecretId<TSecretBucket> FromSqid(string secretId)
    {
        var parts = secretId.FromSqid().Split(':');
        var secretBucket = Enum.Parse<TSecretBucket>(parts[0]);
        var secretType = (SecretType)int.Parse(parts[1], CultureInfo.InvariantCulture);
        var key = parts[2];
        return new SecretId<TSecretBucket>(secretBucket, secretType, key);
    }

    public static bool TryParse(string id, out SecretId<TSecretBucket> secretId)
    {
        try
        {
            secretId = FromSqid(id);
            return true;
        }
        catch
        {
            secretId = default;
            return false;
        }
    }

    public static SecretId<TSecretBucket> New(TSecretBucket bucket, SecretType type) => new(bucket, type, Guid.NewGuid().ToString("N"));
}
