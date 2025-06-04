using Dosaic.Plugins.Persistence.S3.Blob;

namespace Dosaic.Plugins.Persistence.S3.File;

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
}
