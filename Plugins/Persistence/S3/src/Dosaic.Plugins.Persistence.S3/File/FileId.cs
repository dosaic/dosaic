using Dosaic.Extensions.Sqids;

namespace Dosaic.Plugins.Persistence.S3.File;

public interface IFileIdKey
{
    string Key { get; }
}

public static class FileIdExtensions
{
    public static string GenerateSqidId(string bucket, string key) => $"{bucket}:{key}".ToSqid();

    public static (string bucket, string key) SplitFromSqid(string fileId)
    {
        var parts = fileId.FromSqid().Split(':');
        return (parts[0], parts[1]);
    }
}

public readonly struct FileId<TBucket>(TBucket bucket, string key) : IFileIdKey
    where TBucket : struct, Enum
{
    public string Key { get; } = key;
    public TBucket Bucket { get; } = bucket;
    public string Id => FileIdExtensions.GenerateSqidId(Bucket.GetName(), Key);

    public static FileId<TBucket> FromSqid(string fileId)
    {
        var (bucketName, key) = FileIdExtensions.SplitFromSqid(fileId);
        var bucket = Enum.Parse<TBucket>(bucketName, true);
        return new FileId<TBucket>(bucket, key);
    }

    public FileId ToFileId() => new(Bucket.GetName(), Key);

    public static bool TryParse(string inputFileId, out FileId<TBucket> fileId)
    {
        try
        {
            fileId = FromSqid(inputFileId);
            return true;
        }
        catch
        {
            fileId = default;
            return false;
        }
    }

    public static FileId<TBucket> New(TBucket bucket) => new(bucket, Guid.NewGuid().ToString("N"));
}

public readonly struct FileId(string bucket, string key)
    : IFileIdKey
{
    public string Key { get; } = key;
    public string Bucket { get; } = bucket;

    public string Id => FileIdExtensions.GenerateSqidId(Bucket, Key);

    public static FileId FromSqid(string fileId)
    {
        var (bucket, key) = FileIdExtensions.SplitFromSqid(fileId);
        return new FileId(bucket, key);
    }

    public static bool TryParse(string inputFileId, out FileId fileId)
    {
        try
        {
            fileId = FromSqid(inputFileId);
            return true;
        }
        catch
        {
            fileId = default;
            return false;
        }
    }

    public static FileId New(string bucket) => new(bucket, Guid.NewGuid().ToString("N"));
}
