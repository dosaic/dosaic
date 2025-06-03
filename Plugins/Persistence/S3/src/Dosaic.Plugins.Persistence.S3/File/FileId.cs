using Dosaic.Extensions.Sqids;

namespace Dosaic.Plugins.Persistence.S3.File;

public readonly struct FileId<TBucket>(TBucket bucket, string key) where TBucket : struct, Enum
{
    public TBucket Bucket { get; } = bucket;
    public string Key { get; } = key;
    public string Id => $"{Bucket.GetName()}:{Key}".ToSqid();

    public static FileId<TBucket> FromSqid(string fileId)
    {
        var parts = fileId.FromSqid().Split(':');
        var bucket = Enum.Parse<TBucket>(parts[0], true);
        return new(bucket, parts[1]);
    }

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

public readonly struct FileId(string bucket, string key, FileType bucketFileType = FileType.All)
{
    public string Bucket { get; } = bucket;
    public FileType BucketFileType { get; } = bucketFileType;
    public string Key { get; } = key;
    public string Id => $"{Bucket}:{Key}".ToSqid();

    public static FileId FromSqid(string fileId)
    {
        var parts = fileId.FromSqid().Split(':');
        return new(parts[0], parts[1]);
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
