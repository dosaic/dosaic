using System.Globalization;
using System.Security.Cryptography;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using MimeDetective;
using Minio;
using Minio.DataModel.Args;

namespace Dosaic.Plugins.Persistence.S3.File;

public class FileStorage<BucketEnum>(
    IMinioClient minioClient,
    IContentInspector contentInspector,
    ILogger<FileStorage<BucketEnum>> logger) : IFileStorage<BucketEnum> where BucketEnum : struct, Enum
{
    private static readonly SHA256 _sha256 = SHA256.Create();

    public async Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = await _sha256.ComputeHashAsync(stream, cancellationToken);
        var hash = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        stream.Seek(0, SeekOrigin.Begin);
        return hash;
    }

    public async Task<BlobFile<BucketEnum>> GetFileAsync(FileId<BucketEnum> id,
        CancellationToken cancellationToken = default)
    {
        var statArgs = new StatObjectArgs().WithBucket(id.Bucket.GetName()).WithObject(id.Key);
        var objectStat = await minioClient.StatObjectAsync(statArgs, cancellationToken);
        var metaData = new Dictionary<string, string>
        {
            {
                BlobFileMetaData.Filename,
                objectStat.MetaData.GetValueOrDefault(BlobFileMetaData.Filename, objectStat.ObjectName)
            },
            { BlobFileMetaData.ETag, $"\"{objectStat.ETag}\"" },
            { BlobFileMetaData.ContentType, objectStat.ContentType },
            { BlobFileMetaData.ContentLength, objectStat.Size.ToString(CultureInfo.InvariantCulture) }
        };
        if (objectStat.MetaData.TryGetValue(BlobFileMetaData.Hash, out var hashValue))
            metaData.Add(BlobFileMetaData.Hash, hashValue);
        return new BlobFile<BucketEnum> { Id = id, LastModified = objectStat.LastModified, MetaData = metaData };
    }

    public Task DeleteFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default)
    {
        return minioClient.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(id.Bucket.GetName()).WithObject(id.Key),
            cancellationToken);
    }

    public async Task ConsumeStreamAsync(FileId<BucketEnum> id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default)
    {
        var getArgs = new GetObjectArgs().WithBucket(id.Bucket.GetName()).WithObject(id.Key)
            .WithCallbackStream(streamConsumer);
        await minioClient.GetObjectAsync(getArgs, cancellationToken);
    }

    public async Task<FileId<BucketEnum>> SetAsync(BlobFile<BucketEnum> file, Stream stream,
        CancellationToken cancellationToken = default)
    {
        file.MetaData[BlobFileMetaData.ContentType] = GetMimeType(file.Id, stream);
        file.MetaData[BlobFileMetaData.Hash] = await ComputeHash(stream, cancellationToken);

        var arguments = new PutObjectArgs()
            .WithBucket(file.Id.Bucket.GetName())
            .WithObject(file.Id.Key)
            .WithHeaders(file.MetaData)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(file.MetaData[BlobFileMetaData.ContentType]);

        var result = await minioClient.PutObjectAsync(arguments, cancellationToken);
        if (result != null)
        {
            logger.LogDebug("Put {Bucket}:{Object} into S3", file.Id.Key, file.Id.Bucket);
            return file.Id;
        }

        var errorMessage = $"Could not save file {file.Id.Bucket}:{file.Id.Key} to s3";
        logger.LogError(errorMessage);
        throw new DosaicException(errorMessage);
    }


    private string GetMimeType(FileId<BucketEnum> fileId, Stream stream)
    {
        var result = contentInspector.Inspect(stream).FirstOrDefault();
        if (result == null)
            throw new ValidationDosaicException(typeof(BlobFile<BucketEnum>),
                "Could not determine content type, abort processing.");
        var allowedDefinitions = fileId.Bucket.GetDefinitions();
        if (!allowedDefinitions.Select(x => x.File.MimeType)
                .Contains(result.Definition.File.MimeType))
        {
            throw new ValidationDosaicException(typeof(BlobFile<BucketEnum>),
                $"Invalid file format. Only {string.Join(",", allowedDefinitions.Select(x => x.File.MimeType))} allowed!");
        }

        stream.Seek(0, SeekOrigin.Begin);
        return result.Definition.File.MimeType!;
    }
}
