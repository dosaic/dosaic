# Dosaic.Plugins.Persistence.S3

Dosaic.Plugins.Persistence.S3 is a plugin that allows other Dosaic components to interact with S3-compatible storage.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Persistence.S3
```

or add as package reference to your .csproj

```xml

<PackageReference Include="Dosaic.Plugins.Persistence.S3" Version=""/>
```

## Appsettings.yml

Configure your appsettings.yml with these properties:

```yaml
s3:
  endpoint: ""
  bucketPrefix: "" # optional, used to prefix all bucket names
  accessKey: ""
  secretKey: ""
  region: ""
  useSsl: true
  healthCheckPath: ""
```

## Registration and Configuration

### File Storage with pre-defined buckets

To use the file storage functionality with a pre-defined bucket list, define an enum for your buckets:

```csharp
public enum MyBucket
{
    [FileBucket("logos", FileType.Images)]
    Logos = 0,

    [FileBucket("avatars", FileType.Images)]
    Avatars = 1,

    [FileBucket("docs", FileType.Documents)]
    Documents = 3
}
```

Then register the file storage for your bucket enum:

```csharp
services.AddFileStorage<MyBucket>();
```

This registers `IFileStorage<MyBucket>` which can be injected into your services.

#### Automatic Bucket Migration Service

To ensure buckets are created automatically when your application starts, register the migration service:
The service will automatically create all buckets defined in your enum.

```csharp
// Register migration service for specific buckets
services.AddBlobStorageBucketMigrationService(MyBucket.Logos);
services.AddBlobStorageBucketMigrationService(MyOtherBucket.Cars);
```

### File Storage without enum based buckets

The plugin automatically registers IFilestorage with the service collection.

**When using `IFilestorage` instead of `IFilestorage<MyBucket>`, there is no bucket migration service since, we don't
know what buckets should exist at runtime.**

Therefor you must create your bucket at runtime

```csharp
public class FileProvider(IFileStorage fileStorage)
{
    await fileStorage.CreateBucketAsync("mybucket", cancellationToken);
}
```

### Basic setup without a dosaic web host (optional)

If you don't use the dosaic webhost,
which automatically configures the DI container,
you'll need to register the S3 plugin manually:

```csharp
services.AddS3BlobStoragePlugin(new S3Configuration
{
    Endpoint = "s3.example.com",
    BucketPrefix = "myapp-", // optional, used to prefix all bucket names
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    Region = "us-west-1", // optional
    UseSsl = true,        // optional
    HealthCheckPath = ""  // optional
});
```

## Custom mimetype definitions

### Filetype Definitions

You can define/override custom definitions for each `Filetype` by implementing the `IFileTypeDefinitionResolver`
interface.

```csharp
  internal class EmptyFileTypeDefinitionResolver : IFileTypeDefinitionResolver
    {
        public ImmutableArray<Definition> GetDefinitions(FileType fileType)
        {
            return ImmutableArray<Definition>.Empty;
        }
    }
```

The default implementation is `DefaultFileTypeDefinitionResolver` can uses all the default definitions from class
`MimeDetective.Definitions.DefaultDefinitions`.

### ContentInspector Definitions

You can define/override definitions the content inspector by replacing the `IContentInspector` in the IoC with your own
implementation.

Example

```csharp
serviceCollection.Replace(ServiceDescriptor.Singleton<IContentInspector>(sp =>
new ContentInspectorBuilder
    {
        Definitions = DefaultDefinitions.All()
            .Where(x => x.File.Extensions.Contains("pdf")).ToList() // only use pdf defitions
    }
.Build()));
```

The plugin uses by default `Definitions = DefaultDefinitions.All()`.

## Working with Files

### Blob file creation

```csharp
// sets original-filename metadata
// sets fileExtension metadata
new BlobFile<MyBucket>(MyBucket.Logos, "someKey").WithFilename("myfile.pdf")
// sets fileExtension metadata
// sets custom metadata
new BlobFile<MyBucket>(MyBucket.Logos, "someKey").WithFileExtension(".pdf")
{
     MetaData = new Dictionary<string, string>
        {
            { "something-custom", "test" }
        },
    LastModified = DateTimeOffset.UtcNow
}
```

### Mimetype detection

If the `MetaData[BlobFileMetaData.ContentType]` of the `BlobFile` is not set,
the plugin will automatically try to detect the mimetype in the following order:

1. If the `MetaData[BlobFileMetaData.FileExtension]` is set, it will use the `IFileTypeDefinitionResolver` to get the
   mimetype.
2. If the `MetaData[BlobFileMetaData.FileExtension]` is not set, it will use the `IContentInspector` to detect the
   mimetype based on the file content.
3. If the mimetype cannot be detected, it will default to `application/octet-stream`.

### Validation

Validation depends on the `FileType` of the `SetAsync()` method and the detected mimetype result in
`MetaData[BlobFileMetaData.ContentType]`.

If `FileType.Any` is used, no validation is performed.

Otherwise, the detected mimetype must match one of the allowed mimetypes defined in the `FileType` enum (can be
customized via `IFileTypeDefinitionResolver`).

### Usage with permission checks or acl's

Example of using the file storage interface:

```csharp
public class FileProvider(IFileStorage<MyBucket> fileStorage)
{
    private Task CheckPermissionAsync(FileId fileId, CancellationToken cancellationToken)
    {
        // check permissions or other logic
        if (permission == null)
            throw Exception("Could not find requested file");
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
        await CheckPermissionAsync(id, cancellationToken);
        return fileStorage.SetAsync(file, stream, cancellationToken);
    }

    public async Task DeleteFileAsync(FileId<MyBucket> id, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        await fileStorage.DeleteFileAsync(id, cancellationToken);
    }
}

```

### Example usage in a controller

```csharp
[[ApiController, Route("/files"), Authorize]
public class FilesController(IFileStorage<MyBucket> fileStorage) : ControllerBase
{
    [HttpGet("{key:required}")]
    public async Task<IResult> GetFileByKeyAsync([FromRoute] string key, CancellationToken cancellationToken)
    {
        if (!FileId.TryParse(key, out var fileId))
            return Results.StatusCode(StatusCodes.Status404NotFound);
        var file = await fileStorage.GetFileAsync(fileId, cancellationToken);
        var etag = file.MetaData[BlobFileMetaData.ETag];
        var lastModified = file.LastModified;

        if (CheckIfResponseIsNotModified(etag, lastModified))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        var fileName = file.MetaData.TryGetValue(BlobFileMetaData.Filename, out var value) ? value : fileId.Id;

        Response.Headers.Append("Content-Length", file.MetaData[BlobFileMetaData.ContentLength]);
        Response.Headers.Append("Cache-Control", "private, max-age=300, immutable, must-revalidate");

        return Results.Stream(sr => fileStorage.ConsumeStreamAsync(fileId, async (stream, ct) => await stream.CopyToAsync(sr, ct), cancellationToken), file.MetaData[BlobMetaData.ContentType], fileName, lastModified, new EntityTagHeaderValue(etag));
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



