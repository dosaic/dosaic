using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Blob;

public abstract class BaseBlobFile
{
    public Dictionary<string, string> MetaData { get; set; } = new();
    public DateTimeOffset LastModified { get; set; }

    protected static Dictionary<string, string> GetMetaData(string fileName)
    {
        return fileName == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { { BlobFileMetaData.Filename, fileName } };
    }
}

public class BlobFile<BucketEnum> : BaseBlobFile where BucketEnum : struct, Enum
{
    public FileId<BucketEnum> Id { get; set; }

    public static BlobFile<BucketEnum> Create(FileId<BucketEnum> id, string fileName = null)
    {
        return new BlobFile<BucketEnum>
        {
            Id = id, MetaData = GetMetaData(fileName), LastModified = DateTimeOffset.UtcNow
        };
    }
}

public class BlobFile : BaseBlobFile
{
    public FileId Id { get; set; }

    public static BlobFile Create(FileId id, string fileName = null)
    {
        return new BlobFile { Id = id, MetaData = GetMetaData(fileName), LastModified = DateTimeOffset.UtcNow };
    }
}
