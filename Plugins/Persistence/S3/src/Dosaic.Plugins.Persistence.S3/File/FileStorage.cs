using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Microsoft.Extensions.Logging;
using MimeDetective;
using MimeDetective.Storage;

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

        var blob = new BlobFile<BucketEnum>(id.Bucket, file.Id.Key) { LastModified = file.LastModified };

        blob.MetaData.Set(file.MetaData.GetMetadata());

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
        blob.MetaData.Set(file.MetaData.GetMetadata());
        var fileId = await fileStorage.SetAsync(blob,
            stream, file.Id.Bucket.GetFileType(), cancellationToken);

        return new FileId<BucketEnum>(file.Id.Bucket, fileId.Key);
    }

    public IAsyncEnumerable<FileListItem<BucketEnum>> ListObjectsAsync(BucketEnum bucket, ListObjectOptions options,
        CancellationToken cancellationToken = default)
    {
        return fileStorage.ListObjectsAsync(bucket.GetName(), options, cancellationToken)
            .Select(item => new FileListItem<BucketEnum>(new FileId<BucketEnum>(bucket, item.FileId.Key), item.ETag,
                item.Size, item.LastModified, item.IsDirectory));
    }
}

public class FileStorage(
    IAmazonS3 s3Client,
    IContentInspector contentInspector,
    ILogger<FileStorage> logger,
    S3Configuration configuration,
    IFileTypeDefinitionResolver fileTypeDefinitionResolver) : IFileStorage
{
    private const string ApplicationOctetStream = "application/octet-stream";
    private const string MetadataPrefix = "x-amz-meta-";

    private static void SetFileIdTags(Activity activity, FileId fileId)
    {
        activity?.SetTag("file.bucket", fileId.Bucket);
        activity?.SetTag("file.key", fileId.Key);
    }

    public async Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hash = Convert.ToHexStringLower(bytes);
        stream.Seek(0, SeekOrigin.Begin);
        return hash;
    }

    public Task<BlobFile> GetFileAsync(FileId id,
        CancellationToken cancellationToken = default) => Tracing.TrackStatusAsync(async (activity) =>
    {
        SetFileIdTags(activity, id);
        var objectStat = await s3Client.GetObjectMetadataAsync(
            new GetObjectMetadataRequest { BucketName = ResolveBucketName(id.Bucket), Key = id.Key },
            cancellationToken);
        var objectMetaData = GetObjectMetaData(objectStat);
        var metaData = new Dictionary<string, string>
        {
            {
                BlobFileMetaData.Filename,
                objectMetaData.GetValueOrDefault(BlobFileMetaData.Filename, id.Key)
            },
            { BlobFileMetaData.ETag, $"\"{objectStat.ETag?.Trim('"')}\"" },
            { BlobFileMetaData.ContentType, objectStat.Headers.ContentType },
            { BlobFileMetaData.ContentLength, objectStat.ContentLength.ToString(CultureInfo.InvariantCulture) }
        };
        activity.SetTags(metaData, "file.metadata.");
        if (objectMetaData.TryGetValue(BlobFileMetaData.Hash, out var hashValue))
            metaData.Add(BlobFileMetaData.Hash, hashValue);
        var blob = new BlobFile(id) { LastModified = ToDateTimeOffset(objectStat.LastModified) };
        blob.MetaData.Set(metaData);
        return blob;
    });

    private static Dictionary<string, string> GetObjectMetaData(GetObjectMetadataResponse objectStat)
    {
        var metaData = new Dictionary<string, string>();
        foreach (var key in objectStat.Metadata.Keys)
        {
            var strippedKey = key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)
                ? key[MetadataPrefix.Length..]
                : key;
            metaData[strippedKey] = objectStat.Metadata[key];
        }

        return metaData;
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime? value) =>
        value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : DateTimeOffset.MinValue;

    public Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default) =>
        Tracing.TrackStatusAsync((
            activity) =>
        {
            SetFileIdTags(activity, id);
            if (configuration.SkipFileDeletion)
            {
                return Task.CompletedTask;
            }

            return s3Client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = ResolveBucketName(id.Bucket), Key = id.Key },
                cancellationToken);
        });

    public Task CreateBucketAsync(string bucket, CancellationToken cancellationToken = default) =>
        Tracing.TrackStatusAsync(async (activity) =>
        {
            var bucketName = ResolveBucketName(bucket);
            activity?.SetTag("s3.bucket", bucketName);
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, cancellationToken);
        });

    public async Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default) => await Tracing.TrackStatusAsync(async (activity) =>
    {
        SetFileIdTags(activity, id);
        using var response = await s3Client.GetObjectAsync(
            new GetObjectRequest { BucketName = ResolveBucketName(id.Bucket), Key = id.Key }, cancellationToken);
        await using var stream = response.ResponseStream;
        await streamConsumer(stream, cancellationToken);
    });

    public async Task<FileId> SetAsync(BlobFile file, Stream stream, FileType fileType,
        CancellationToken cancellationToken = default) => await Tracing.TrackStatusAsync(async (activity) =>
    {
        SetFileIdTags(activity, file.Id);
        if (!file.MetaData.ContainsKey(BlobFileMetaData.ContentType))
        {
            file.MetaData.TryGetValue(BlobFileMetaData.FileExtension, out var fileExtension);
            var contentType = string.IsNullOrEmpty(fileExtension)
                ? GetMimeTypeFromContent(stream) ?? ApplicationOctetStream
                : GetMimeTypeFromFileExtension(fileExtension) ?? ApplicationOctetStream;

            file.MetaData.Set(BlobFileMetaData.ContentType, contentType);
        }

        file.MetaData.Set(BlobFileMetaData.Hash, await ComputeHash(stream, cancellationToken));

        activity?.SetTag("file.type", fileType);
        activity.SetTags(file.MetaData.GetMetadata(), "file.metadata.");

        ValidateContentType(fileType, file.MetaData[BlobFileMetaData.ContentType]);

        var bucketWithPrefix = ResolveBucketName(file.Id.Bucket);
        var request = new PutObjectRequest
        {
            BucketName = bucketWithPrefix,
            Key = file.Id.Key,
            InputStream = stream,
            AutoCloseStream = false,
            ContentType = file.MetaData[BlobFileMetaData.ContentType]
        };
        foreach (var (key, value) in file.MetaData.GetUrlEncodedMetadata())
            request.Metadata.Add(key, value);

        var result = await s3Client.PutObjectAsync(request, cancellationToken);
        if (result is not null && (int)result.HttpStatusCode < 300)
        {
            logger.LogDebug("Put {Bucket}:{Object} into S3", file.Id.Key, bucketWithPrefix);
            return file.Id;
        }

        var errorMessage = $"Could not save file {bucketWithPrefix}:{file.Id.Key} to s3";
        logger.LogError(errorMessage);
        throw new DosaicException(errorMessage);
    });

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

    public async IAsyncEnumerable<FileListItem> ListObjectsAsync(string bucket, ListObjectOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = ResolveBucketName(bucket),
            Prefix = string.IsNullOrEmpty(options.Prefix) ? null : options.Prefix,
            Delimiter = options.Recursive ? null : "/"
        };
        do
        {
            var response = await s3Client.ListObjectsV2Async(request, cancellationToken);
            foreach (var prefix in response.CommonPrefixes ?? [])
                yield return new FileListItem(new FileId(bucket, prefix), "", 0, DateTimeOffset.MinValue, true);
            foreach (var item in response.S3Objects ?? [])
                yield return new FileListItem(new FileId(bucket, item.Key), item.ETag?.Trim('"') ?? "",
                    item.Size ?? 0, ToDateTimeOffset(item.LastModified), false);
            request.ContinuationToken = response.NextContinuationToken;
        } while (!string.IsNullOrEmpty(request.ContinuationToken));
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
