using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Blob;

public class BlobFile<BucketEnum> where BucketEnum : struct, Enum
{
    public FileId<BucketEnum> Id { get; set; }
    public Dictionary<string, string> MetaData { get; set; } = new();
    public DateTimeOffset LastModified { get; set; }

    public static BlobFile<BucketEnum> Create(FileId<BucketEnum> id, string fileName = null)
    {
        var metadata = fileName == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { { BlobFileMetaData.Filename, fileName } };
        return new BlobFile<BucketEnum> { Id = id, MetaData = metadata, LastModified = DateTimeOffset.UtcNow };
    }
}
