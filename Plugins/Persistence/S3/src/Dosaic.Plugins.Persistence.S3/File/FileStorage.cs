using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Microsoft.Extensions.Logging;
using MimeDetective;
using MimeDetective.Storage;
using Minio;
using Minio.DataModel.Args;

namespace Dosaic.Plugins.Persistence.S3.File;

public class FileStorage<BucketEnum>(
    IFileStorage fileStorage
) : IFileStorage<BucketEnum> where BucketEnum : struct, Enum
{
    public async Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        return await fileStorage.ComputeHash(stream, cancellationToken);
    }

    public async Task<BlobFile<BucketEnum>> GetFileAsync(FileId<BucketEnum> id,
        CancellationToken cancellationToken = default)
    {
        var file = await fileStorage.GetFileAsync(id.ToFileId(), cancellationToken);

        var blob = new BlobFile<BucketEnum>(id.Bucket, file.Id.Key)
        {
            LastModified = file.LastModified
        };

        blob.AddMetaData(file.MetaData);

        return blob;
    }

    public Task DeleteFileAsync(FileId<BucketEnum> id, CancellationToken cancellationToken = default)
    {
        return fileStorage.DeleteFileAsync(id.ToFileId(), cancellationToken);
    }

    public async Task ConsumeStreamAsync(FileId<BucketEnum> id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default)
    {
        await fileStorage.ConsumeStreamAsync(id.ToFileId(), streamConsumer,
            cancellationToken);
    }

    public async Task<FileId<BucketEnum>> SetAsync(BlobFile<BucketEnum> file, Stream stream,
        CancellationToken cancellationToken = default)
    {
        var blob = new BlobFile(file.Id.ToFileId()) { LastModified = file.LastModified };
        blob.AddMetaData(file.MetaData);
        var fileId = await fileStorage.SetAsync(blob,
            stream, file.Id.Bucket.GetFileType(), cancellationToken);

        return new FileId<BucketEnum>(file.Id.Bucket, fileId.Key);
    }
}

public class FileStorage(
    IMinioClient minioClient,
    IContentInspector contentInspector,
    ILogger<FileStorage> logger,
    S3Configuration configuration,
    IFileTypeDefinitionResolver fileTypeDefinitionResolver) : IFileStorage
{
    private const string ApplicationOctetStream = "application/octet-stream";
    public async Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hash = Convert.ToHexStringLower(bytes);
        stream.Seek(0, SeekOrigin.Begin);
        return hash;
    }

    public async Task<BlobFile> GetFileAsync(FileId id,
        CancellationToken cancellationToken = default)
    {
        var statArgs = new StatObjectArgs().WithBucket(ResolveBucketName(id.Bucket)).WithObject(id.Key);
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
        var blob = new BlobFile(id) { LastModified = objectStat.LastModified };
        blob.AddMetaData(metaData);
        return blob;
    }

    public Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default)
    {
        return minioClient.RemoveObjectAsync(
            new RemoveObjectArgs().WithBucket(ResolveBucketName(id.Bucket)).WithObject(id.Key),
            cancellationToken);
    }

    public async Task CreateBucketAsync(string bucket, CancellationToken cancellationToken = default)
    {
        await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(ResolveBucketName(bucket)),
            cancellationToken);
    }

    public async Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default)
    {
        var getArgs = new GetObjectArgs().WithBucket(ResolveBucketName(id.Bucket)).WithObject(id.Key)
            .WithCallbackStream(streamConsumer);
        await minioClient.GetObjectAsync(getArgs, cancellationToken);
    }

    public async Task<FileId> SetAsync(BlobFile file, Stream stream, FileType fileType,
        CancellationToken cancellationToken = default)
    {
        if (!file.EncodedMetaData.ContainsKey(BlobFileMetaData.ContentType))
        {
            file.EncodedMetaData.TryGetValue(BlobFileMetaData.FileExtension, out var fileExtension);
            file.EncodedMetaData[BlobFileMetaData.ContentType] = string.IsNullOrEmpty(fileExtension)
                ? GetMimeTypeFromContent(stream) ?? ApplicationOctetStream
                : GetMimeTypeFromFileExtension(fileExtension) ?? ApplicationOctetStream;
        }

        ValidateContentType(fileType, file.EncodedMetaData[BlobFileMetaData.ContentType]);

        file.EncodedMetaData[BlobFileMetaData.Hash] = await ComputeHash(stream, cancellationToken);

        var bucketWithPrefix = ResolveBucketName(file.Id.Bucket);
        var arguments = new PutObjectArgs()
            .WithBucket(bucketWithPrefix)
            .WithObject(file.Id.Key)
            .WithHeaders(file.EncodedMetaData)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(file.EncodedMetaData[BlobFileMetaData.ContentType]);

        var result = await minioClient.PutObjectAsync(arguments, cancellationToken);
        if (result != null)
        {
            logger.LogDebug("Put {Bucket}:{Object} into S3", file.Id.Key, bucketWithPrefix);
            return file.Id;
        }

        var errorMessage = $"Could not save file {bucketWithPrefix}:{file.Id.Key} to s3";
        logger.LogError(errorMessage);
        throw new DosaicException(errorMessage);
    }

    private string GetMimeTypeFromFileExtension(string filename)
    {
        var fileExtension = Path.GetExtension(filename);
        return GetDefinitions(FileType.All)
            .FirstOrDefault(x => x.File.Extensions.Any(e => e == fileExtension.Trim('.')))?.File
            .MimeType;
    }

    internal ImmutableArray<Definition> GetDefinitions(FileType fileType)
    {
        var definitions = new List<Definition>();

        foreach (FileType type in Enum.GetValues(typeof(FileType)))
        {
            if (!fileType.HasFlag(type) || type == FileType.Any)
                continue;
            definitions.AddRange(fileTypeDefinitionResolver.GetDefinitions(type));
        }

        return [.. definitions];
    }

    private string GetMimeTypeFromContent(Stream stream)
    {
        var result = contentInspector.Inspect(stream).FirstOrDefault();
        stream.Seek(0, SeekOrigin.Begin);
        return result?.Definition.File.MimeType;
    }

    public string ResolveBucketName(string bucket)
    {
        return $"{configuration.BucketPrefix}{bucket}";
    }

    private void ValidateContentType(FileType fileType, string contentType)
    {
        if (fileType == FileType.Any) return;
        var allowedDefinitions = GetDefinitions(fileType);
        if (!allowedDefinitions.Select(x => x.File.MimeType)
                .Contains(contentType))
        {
            throw new ValidationDosaicException(typeof(BlobFile),
                $"Invalid file format. Only {string.Join(",", allowedDefinitions.Select(x => x.File.MimeType))} allowed!");
        }
    }
}
