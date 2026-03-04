# Dosaic.Plugins.Persistence.S3

`Dosaic.Plugins.Persistence.S3` is a plugin that provides S3-compatible object storage for Dosaic applications. It wraps the [Minio](https://github.com/minio/minio-dotnet) client, adds automatic MIME-type detection via [Mime-Detective](https://github.com/MediatedCommunications/Mime-Detective), bucket-prefixing, SHA-256 hashing, OpenTelemetry tracing, and a local-filesystem fallback for development and testing.

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.S3
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.S3" Version=""/>
```

## Configuration

### `appsettings.yml`

```yaml
s3:
  endpoint: "s3.example.com"         # S3 / MinIO endpoint (host[:port])
  accessKey: "your-access-key"
  secretKey: "your-secret-key"
  region: "us-east-1"                # optional
  useSsl: true                       # optional, default false
  bucketPrefix: "myapp-"             # optional, prefixed to every bucket name
  healthCheckPath: ""                # optional, path appended to endpoint URL for readiness check
  useLocalFileSystem: false          # optional, use local filesystem instead of S3 (dev/test mode)
  localFileSystemPath: "./nodep-s3"  # optional, root path used when useLocalFileSystem is true
```

When `useLocalFileSystem: true` the plugin stores files on the local disk at `localFileSystemPath` instead of connecting to an S3 endpoint. This is useful for local development and integration tests where no MinIO/S3 instance is available.

## Registration and Configuration

The plugin is automatically discovered and registered by the Dosaic source generator when using `PluginWebHostBuilder`. No manual registration is required in that case.

### Enum-based typed buckets (recommended)

Define an enum whose values are annotated with `[FileBucket]`. The attribute declares the bucket name and the allowed `FileType` for validation:

```csharp
public enum MyBucket
{
    [FileBucket("logos", FileType.Images)]
    Logos = 0,

    [FileBucket("avatars", FileType.Images)]
    Avatars = 1,

    [FileBucket("docs", FileType.Documents)]
    Documents = 2,
}
```

Then register `IFileStorage<MyBucket>` in DI:

```csharp
// Storage only
services.AddFileStorage<MyBucket>();

// Storage + automatic bucket-creation on startup (recommended for production)
services.AddFileStorageWithBucketMigration<MyBucket>();

// Or register them separately
services.AddFileStorage<MyBucket>();
services.AddBlobStorageBucketMigrationService<MyBucket>();
```

`IFileStorage<MyBucket>` can then be injected anywhere in your application.

### Untyped bucket storage

The plugin also registers an untyped `IFileStorage`. Because there is no enum to inspect, **no bucket migration service exists for this interface** — you must create buckets manually at runtime:

```csharp
public class FileProvider(IFileStorage fileStorage)
{
    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        await fileStorage.CreateBucketAsync("my-bucket", cancellationToken);
    }
}
```

### Manual registration without Dosaic WebHost

```csharp
services.AddS3BlobStoragePlugin(new S3Configuration
{
    Endpoint = "s3.example.com",
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    BucketPrefix = "myapp-",   // optional
    Region = "us-east-1",      // optional
    UseSsl = true,             // optional
    HealthCheckPath = "",      // optional
});
```

## Usage

### Creating a `BlobFile`

`BlobFile<TBucket>` carries the file metadata and the target bucket/key. Use the fluent helpers to attach filename or extension metadata:

```csharp
// Auto-generated key (UUID), sets original-filename and file-extension metadata
var file = new BlobFile<MyBucket>(MyBucket.Logos).WithFilename("company-logo.png");

// Explicit key, sets only file-extension metadata
var file = new BlobFile<MyBucket>(MyBucket.Logos, "my-custom-key")
    .WithFileExtension(".pdf");
file.MetaData[BlobFileMetaData.ContentType] = "application/pdf"; // override content-type

// Generate a new random FileId directly
var fileId = FileId<MyBucket>.New(MyBucket.Logos);
```

### Upload a file

```csharp
public class FileService(IFileStorage<MyBucket> fileStorage)
{
    public async Task<string> UploadLogoAsync(Stream stream, string originalName,
        CancellationToken cancellationToken = default)
    {
        var file = new BlobFile<MyBucket>(MyBucket.Logos).WithFilename(originalName);
        var fileId = await fileStorage.SetAsync(file, stream, cancellationToken);
        // fileId.Id is the Sqids-encoded public identifier (bucket + key)
        return fileId.Id;
    }
}
```

### Download file metadata

```csharp
public async Task<BlobFile<MyBucket>> GetMetadataAsync(string encodedId,
    CancellationToken cancellationToken = default)
{
    if (!FileId<MyBucket>.TryParse(encodedId, out var fileId))
        throw new ArgumentException("Invalid file id.");

    return await fileStorage.GetFileAsync(fileId, cancellationToken);
}
```

### Stream file content

```csharp
public async Task DownloadAsync(string encodedId, Stream destination,
    CancellationToken cancellationToken = default)
{
    if (!FileId<MyBucket>.TryParse(encodedId, out var fileId))
        throw new ArgumentException("Invalid file id.");

    await fileStorage.ConsumeStreamAsync(fileId,
        async (stream, ct) => await stream.CopyToAsync(destination, ct),
        cancellationToken);
}
```

### Delete a file

```csharp
public async Task DeleteAsync(string encodedId, CancellationToken cancellationToken = default)
{
    if (!FileId<MyBucket>.TryParse(encodedId, out var fileId))
        throw new ArgumentException("Invalid file id.");

    await fileStorage.DeleteFileAsync(fileId, cancellationToken);
}
```

### Compute a SHA-256 hash

Both `IFileStorage` and `IFileStorage<TBucket>` implement `IComputeHash`. The hash is also stored automatically in `BlobFileMetaData.Hash` when a file is uploaded.

```csharp
string hash = await fileStorage.ComputeHash(stream, cancellationToken);
```

## Custom MIME-type definitions

### Override `IFileTypeDefinitionResolver`

Implement `IFileTypeDefinitionResolver` and register it to replace the default definitions:

```csharp
internal class PdfOnlyFileTypeDefinitionResolver : IFileTypeDefinitionResolver
{
    public ImmutableArray<Definition> GetDefinitions(FileType fileType)
    {
        return DefaultDefinitions.FileTypes.Documents.All()
            .Where(x => x.File.Extensions.Contains("pdf"))
            .ToImmutableArray();
    }
}

// Registration
services.ReplaceDefaultFileTypeDefinitionResolver<PdfOnlyFileTypeDefinitionResolver>();
```

The built-in implementation is `DefaultFileTypeDefinitionResolver`, which delegates to `MimeDetective.Definitions.DefaultDefinitions`.

### Override `IContentInspector`

The content inspector is used for binary MIME detection when no file extension is available:

```csharp
// Replace with a custom definition list
services.ReplaceContentInspector(
    DefaultDefinitions.All()
        .Where(x => x.File.Extensions.Contains("pdf"))
        .ToList());

// Or replace the full singleton
services.Replace(ServiceDescriptor.Singleton<IContentInspector>(_ =>
    new ContentInspectorBuilder
    {
        Definitions = DefaultDefinitions.All()
            .Where(x => x.File.Extensions.Contains("pdf"))
            .ToList()
    }.Build()));
```

## MIME type detection and validation

When `BlobFileMetaData.ContentType` is **not** set on a `BlobFile`, the plugin detects it automatically in this order:

1. If `BlobFileMetaData.FileExtension` is set → look up via `IFileTypeDefinitionResolver`.
2. Otherwise → pass the stream bytes through `IContentInspector`.
3. If still unresolved → fall back to `application/octet-stream`.

After detection, the content-type is validated against the `FileType` declared on the `[FileBucket]` attribute. If they do not match, a `ValidationDosaicException` is thrown. Use `FileType.Any` to skip validation entirely.

## Metadata keys (`BlobFileMetaData`)

| Constant | Key | Description |
|---|---|---|
| `BlobFileMetaData.Filename` | `original-filename` | Original file name |
| `BlobFileMetaData.FileExtension` | `original-file-extension` | File extension (e.g. `.pdf`) |
| `BlobFileMetaData.ContentType` | `content-type` | MIME type |
| `BlobFileMetaData.ContentLength` | `content-length` | File size in bytes |
| `BlobFileMetaData.ETag` | `etag` | S3 ETag (quoted) |
| `BlobFileMetaData.Hash` | `hash` | SHA-256 hex digest (auto-computed on upload) |

## FileId encoding

`FileId` and `FileId<TBucket>` encode the bucket name and object key as a single [Sqids](https://sqids.org/)-encoded string accessible via the `.Id` property. This opaque identifier is safe to expose in URLs and query strings.

```csharp
// Parse an incoming opaque id
if (!FileId<MyBucket>.TryParse(incomingId, out var fileId))
    return Results.NotFound();

// Generate a new random id
var newFileId = FileId<MyBucket>.New(MyBucket.Logos);
Console.WriteLine(newFileId.Id);     // e.g. "aBcDeFgH"
Console.WriteLine(newFileId.Key);    // the raw UUID key
Console.WriteLine(newFileId.Bucket); // MyBucket.Logos
```

### Permission-guarded service wrapper

Example of wrapping the storage interface with permission checks:

```csharp
public class FileProvider(IFileStorage<MyBucket> fileStorage)
{
    private Task CheckPermissionAsync(FileId<MyBucket> fileId, CancellationToken cancellationToken)
    {
        // check permissions or ACL
        return Task.CompletedTask;
    }

    public async Task<BlobFile<MyBucket>> GetFileAsync(FileId<MyBucket> id, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        return await fileStorage.GetFileAsync(id, cancellationToken);
    }

    public async Task ConsumeStreamAsync(FileId<MyBucket> id, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        await fileStorage.ConsumeStreamAsync(id, streamConsumer, cancellationToken);
    }

    public async Task<FileId<MyBucket>> SetAsync(BlobFile<MyBucket> file, Stream stream, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(file.Id, cancellationToken);
        return await fileStorage.SetAsync(file, stream, cancellationToken);
    }

    public async Task DeleteFileAsync(FileId<MyBucket> id, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        await fileStorage.DeleteFileAsync(id, cancellationToken);
    }
}
```

### Example: file download controller

```csharp
[ApiController, Route("/files"), Authorize]
public class FilesController(IFileStorage<MyBucket> fileStorage) : ControllerBase
{
    [HttpGet("{key:required}")]
    public async Task<IResult> GetFileByKeyAsync([FromRoute] string key, CancellationToken cancellationToken)
    {
        if (!FileId<MyBucket>.TryParse(key, out var fileId))
            return Results.StatusCode(StatusCodes.Status404NotFound);
        var file = await fileStorage.GetFileAsync(fileId, cancellationToken);
        var etag = file.MetaData[BlobFileMetaData.ETag];
        var lastModified = file.LastModified;

        if (CheckIfResponseIsNotModified(etag, lastModified))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        var fileName = file.MetaData.TryGetValue(BlobFileMetaData.Filename, out var value) ? value : fileId.Id;

        Response.Headers.Append("Content-Length", file.MetaData[BlobFileMetaData.ContentLength]);
        Response.Headers.Append("Cache-Control", "private, max-age=300, immutable, must-revalidate");

        return Results.Stream(sr => fileStorage.ConsumeStreamAsync(fileId, async (stream, ct) => await stream.CopyToAsync(sr, ct), cancellationToken), file.MetaData[BlobFileMetaData.ContentType], fileName, lastModified, new EntityTagHeaderValue(etag));
    }

    private bool CheckIfResponseIsNotModified(string etag, DateTimeOffset lastModified)
    {
        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && ifNoneMatch == etag)
            return true;
        return Request.Headers.TryGetValue("If-Modified-Since", out var ifModifiedSince) &&
               DateTime.TryParse(ifModifiedSince, out var modifiedSince) &&
               modifiedSince >= lastModified;
    }
}
```

## Features

- **S3-compatible storage** via the Minio .NET client (works with AWS S3, MinIO, Wasabi, etc.)
- **Local filesystem fallback** (`useLocalFileSystem: true`) for zero-dependency dev/test environments
- **Typed enum-based buckets** with `IFileStorage<TBucket>` and per-bucket `FileType` validation
- **Untyped bucket storage** with `IFileStorage` and runtime `CreateBucketAsync`
- **Automatic MIME detection** from file extension or stream content via Mime-Detective
- **Automatic SHA-256 hashing** stored as object metadata on upload
- **Bucket prefix support** to namespace all buckets per environment
- **Automatic bucket migration** via `BlobStorageBucketMigrationService<T>` (hosted background service with retry)
- **Opaque file IDs** using Sqids encoding (bucket + key → single URL-safe string)
- **OpenTelemetry tracing** on all storage operations via `DosaicDiagnostic`
- **Readiness health check** — URL probe for S3 or filesystem write-test for local mode
- **Replaceable** `IFileTypeDefinitionResolver` and `IContentInspector` for custom MIME handling

