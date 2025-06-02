using Dosaic.Extensions.Sqids;

namespace Dosaic.Plugins.Persistence.S3.File;

public readonly struct FileId<BucketEnum>(BucketEnum bucket, string key) where BucketEnum : struct, Enum
{
    public BucketEnum Bucket { get; } = bucket;
    public string Key { get; } = key;

    public string Id => $"{Bucket.GetName()}:{Key}".ToSqid();

    public static FileId<BucketEnum> FromSqid(string fileId)
    {
        var blobParts = fileId.FromSqid().Split(':');
        var bucket = Enum.Parse<BucketEnum>(blobParts[0],true);
        return new FileId<BucketEnum>(bucket, blobParts[1]);
    }

    public static bool TryParse(string inputFileId, out FileId<BucketEnum> fileId)
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

    public static FileId<BucketEnum> New(BucketEnum bucket) => new(bucket, Guid.NewGuid().ToString("N"));
}

public readonly struct FileId(string bucket, string key, FileType bucketFileType = FileType.All)
{
    public string Bucket { get; } = bucket;
    public FileType BucketFileType { get; } = bucketFileType;
    public string Key { get; } = key;

    public string Id => $"{Bucket}:{Key}".ToSqid();

    public static FileId FromSqid(string fileId)
    {
        var blobParts = fileId.FromSqid().Split(':');
        return new FileId(blobParts[0], blobParts[1]);
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
