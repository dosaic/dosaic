using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Blob;

public abstract class BaseBlobFile
{
    public Dictionary<string, string> MetaData { get; set; } = new();
    public DateTimeOffset LastModified { get; set; }

    protected void ApplyFilename(string filename)
    {
        MetaData[BlobFileMetaData.Filename] = filename;
        ApplyFileExtension(filename);
    }

    protected void ApplyFileExtension(string fileExtension)
    {
        MetaData[BlobFileMetaData.FileExtension] = Path.GetExtension(fileExtension);
    }
}

public class BlobFile<BucketEnum> : BaseBlobFile where BucketEnum : struct, Enum
{
    public BlobFile(BucketEnum bucket, string key = null)
    {
        Id = new FileId<BucketEnum>(bucket, key ?? Guid.NewGuid().ToString());
        LastModified = DateTimeOffset.UtcNow;
    }

    public FileId<BucketEnum> Id { get; set; }

    public BlobFile<BucketEnum> WithFilename(string filename)
    {
        ApplyFilename(filename);
        return this;
    }

    public BlobFile<BucketEnum> WithFileExtension(string fileExtension)
    {
        ApplyFileExtension(fileExtension);
        return this;
    }
}

public class BlobFile : BaseBlobFile
{
    public FileId Id { get; set; }

    public BlobFile(string bucket, string key = null)
    {
        Id = new FileId(bucket, key ?? Guid.NewGuid().ToString());
        LastModified = DateTimeOffset.UtcNow;
    }

    public BlobFile(FileId id)
    {
        Id = new FileId(id.Bucket, id.Key ?? Guid.NewGuid().ToString());
        LastModified = DateTimeOffset.UtcNow;
    }

    public BlobFile WithFilename(string filename)
    {
        ApplyFilename(filename);
        return this;
    }

    public BlobFile WithFileExtension(string fileExtension)
    {
        ApplyFileExtension(fileExtension);
        return this;
    }
}
