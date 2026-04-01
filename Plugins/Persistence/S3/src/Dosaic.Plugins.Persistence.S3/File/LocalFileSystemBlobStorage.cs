using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dosaic.Plugins.Persistence.S3.Blob;

namespace Dosaic.Plugins.Persistence.S3.File;

public class LocalFileSystemBlobStorage(string rootPath, bool skipFileDeletion) : IFileStorage
{
    internal string GetFilePath(string bucket, string key) =>
        Path.Combine(rootPath, ResolveBucketName(bucket), key);

    private static string GetMetaPath(string filePath) => filePath + ".meta.json";

    public async Task<string> ComputeHash(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hash = Convert.ToHexStringLower(bytes);
        stream.Seek(0, SeekOrigin.Begin);
        return hash;
    }

    public async Task<BlobFile> GetFileAsync(FileId id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id.Bucket, id.Key);
        var metaPath = GetMetaPath(filePath);

        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {id.Bucket}/{id.Key}", filePath);

        var blob = new BlobFile(id) { LastModified = System.IO.File.GetLastWriteTimeUtc(filePath) };

        if (System.IO.File.Exists(metaPath))
        {
            var metaJson = await System.IO.File.ReadAllTextAsync(metaPath, cancellationToken);
            var meta = JsonSerializer.Deserialize<Dictionary<string, string>>(metaJson);
            if (meta is not null)
                blob.MetaData.Set(meta);
        }
        else
        {
            blob.MetaData.Set(BlobFileMetaData.Filename, id.Key);
            blob.MetaData.Set(BlobFileMetaData.ContentLength,
                new FileInfo(filePath).Length.ToString(CultureInfo.InvariantCulture));
        }

        return blob;
    }

    public async Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id.Bucket, id.Key);
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {id.Bucket}/{id.Key}", filePath);

        await using var stream = System.IO.File.OpenRead(filePath);
        await streamConsumer(stream, cancellationToken);
    }

    public async Task<FileId> SetAsync(BlobFile file, Stream stream, FileType fileType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(file.Id.Bucket, file.Id.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var hash = await ComputeHash(stream, cancellationToken);
        file.MetaData.Set(BlobFileMetaData.Hash, hash);
        file.MetaData.Set(BlobFileMetaData.ContentLength,
            stream.Length.ToString(CultureInfo.InvariantCulture));

        await using (var fileStream = System.IO.File.Create(filePath))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var metaJson = JsonSerializer.Serialize(file.MetaData.GetMetadata());
        await System.IO.File.WriteAllTextAsync(GetMetaPath(filePath), metaJson, cancellationToken);

        return file.Id;
    }

    public Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default)
    {
        if (skipFileDeletion) return Task.CompletedTask;
        var filePath = GetFilePath(id.Bucket, id.Key);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
        var metaPath = GetMetaPath(filePath);
        if (System.IO.File.Exists(metaPath))
            System.IO.File.Delete(metaPath);

        return Task.CompletedTask;
    }

    public Task CreateBucketAsync(string bucket, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(rootPath, ResolveBucketName(bucket)));
        return Task.CompletedTask;
    }

    public string ResolveBucketName(string bucket) => bucket;

    public async IAsyncEnumerable<FileListItem> ListObjectsAsync(string bucket, ListObjectOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bucketPath = Path.Combine(rootPath, ResolveBucketName(bucket));
        if (!Directory.Exists(bucketPath))
            yield break;

        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var filePath in Directory.EnumerateFiles(bucketPath, "*", searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (filePath.EndsWith(".meta.json"))
                continue;

            var key = Path.GetRelativePath(bucketPath, filePath).Replace('\\', '/');
            if (!string.IsNullOrEmpty(options.Prefix) && !key.StartsWith(options.Prefix))
                continue;

            var info = new FileInfo(filePath);
            yield return new FileListItem(new FileId(bucket, key), null, info.Length, info.LastWriteTimeUtc, false);
        }

        await Task.CompletedTask;
    }
}
