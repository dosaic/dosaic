using Dosaic.Plugins.Persistence.S3.Blob;

namespace Dosaic.Plugins.Persistence.S3.File;

public interface IFileStorage<BucketEnum> where BucketEnum : struct, Enum
{
    Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken = default);

    Task<string> ComputeHash(byte[] bytes, CancellationToken cancellationToken = default)
    {
        using var memStream = new MemoryStream(bytes);
        return ComputeHash(memStream, cancellationToken);
    }

    Task<BlobFile<BucketEnum>> GetFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default);

    Task ConsumeStreamAsync(FileId<BucketEnum> id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default);

    Task<FileId<BucketEnum>> SetAsync(BlobFile<BucketEnum> file, Stream stream,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default);


}

public interface IFileStorage
{
    Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken = default);

    Task<string> ComputeHash(byte[] bytes, CancellationToken cancellationToken = default)
    {
        using var memStream = new MemoryStream(bytes);
        return ComputeHash(memStream, cancellationToken);
    }

    Task<BlobFile> GetFileAsync(FileId id, CancellationToken cancellationToken = default);

    Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default);

    Task<FileId> SetAsync(BlobFile file, Stream stream, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default);

    Task CreateBucketAsync(string bucket, CancellationToken cancellationToken = default);
}
