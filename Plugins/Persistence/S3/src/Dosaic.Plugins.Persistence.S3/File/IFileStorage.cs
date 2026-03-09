using Dosaic.Plugins.Persistence.S3.Blob;

namespace Dosaic.Plugins.Persistence.S3.File;

public abstract record BaseFileListItem(string ETag, long Size, DateTimeOffset LastModified, bool IsDirectory);
public record FileListItem(FileId FileId, string ETag, long Size, DateTimeOffset LastModified, bool IsDir) : BaseFileListItem(ETag, Size, LastModified, IsDir);
public record FileListItem<BucketEnum>(FileId<BucketEnum> FileId, string ETag, long Size, DateTimeOffset LastModified, bool IsDir) : BaseFileListItem(ETag, Size, LastModified, IsDir)
    where BucketEnum : struct, Enum;

public class ListObjectOptions
{
    public string Prefix { get; set; }
    public bool Recursive { get; set; }
}

public interface IComputeHash
{
    Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken = default);

    Task<string> ComputeHash(byte[] bytes, CancellationToken cancellationToken = default)
    {
        using var memStream = new MemoryStream(bytes);
        return ComputeHash(memStream, cancellationToken);
    }
}

public interface IFileStorage<BucketEnum> : IComputeHash where BucketEnum : struct, Enum
{
    Task<BlobFile<BucketEnum>> GetFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default);

    Task ConsumeStreamAsync(FileId<BucketEnum> id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default);

    Task<FileId<BucketEnum>> SetAsync(BlobFile<BucketEnum> file, Stream stream,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default);

    IAsyncEnumerable<FileListItem<BucketEnum>> ListObjectsAsync(BucketEnum bucket, ListObjectOptions options, CancellationToken cancellationToken = default);
    IAsyncEnumerable<FileListItem<BucketEnum>> ListObjectsAsync(BucketEnum bucket, CancellationToken cancellationToken = default) =>
        ListObjectsAsync(bucket, new ListObjectOptions(), cancellationToken);
}

public interface IFileStorage : IComputeHash
{
    Task<BlobFile> GetFileAsync(FileId id, CancellationToken cancellationToken = default);

    Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default);

    Task<FileId> SetAsync(BlobFile file, Stream stream, FileType fileType,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default);

    Task CreateBucketAsync(string bucket, CancellationToken cancellationToken = default);
    string ResolveBucketName(string bucket);

    IAsyncEnumerable<FileListItem> ListObjectsAsync(string bucket, ListObjectOptions options, CancellationToken cancellationToken = default);
    IAsyncEnumerable<FileListItem> ListObjectsAsync(string bucket, CancellationToken cancellationToken = default) => ListObjectsAsync(bucket, new ListObjectOptions(), cancellationToken);
}
