using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.S3.File;

namespace Dosaic.Plugins.Persistence.S3.Blob;

public abstract class BaseBlobFile
{
    internal Dictionary<string, string> EncodedMetaData { get; } = new();

    public Dictionary<string, string> MetaData =>
        EncodedMetaData.ToDictionary(
            kv => kv.Key.FromUrlEncoded(),
            kv => kv.Value.FromUrlEncoded()
        );

    public void AddMetaData(KeyValuePair<string, string> metaData)
    {
        EncodedMetaData.Add(metaData.Key.ToUrlEncoded(), metaData.Value.ToUrlEncoded());
    }
    public void AddMetaData(IEnumerable<KeyValuePair<string, string>> metaData)
    {
        foreach (var item in metaData)
        {
            EncodedMetaData.Add(item.Key.ToUrlEncoded(), item.Value.ToUrlEncoded());
        }
    }
    public DateTimeOffset LastModified { get; set; }

    protected void ApplyFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return;
        EncodedMetaData[BlobFileMetaData.Filename] = filename.ToUrlEncoded();
        ApplyFileExtension(filename);
    }

    protected void ApplyFileExtension(string fileExtension)
    {
        var path = Path.GetExtension(fileExtension);
        if (string.IsNullOrEmpty(path)) return;
        EncodedMetaData[BlobFileMetaData.FileExtension] = path.ToUrlEncoded();
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
