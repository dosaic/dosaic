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
  accessKey: ""
  secretKey: ""
  region: ""
  useSsl: true
  healthCheckPath: ""
```

## Registration and Configuration


### File Storage

To use the file storage functionality, first define an enum for your buckets:

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

### Automatic Bucket Migration Service

To ensure buckets are created automatically when your application starts, register the migration service:
The service will automatically create all buckets defined in your enum.

```csharp
// Register migration service for specific buckets
services.AddBlobStorageBucketMigrationService(MyBucket.Logos);
services.AddBlobStorageBucketMigrationService(MyOtherBucket.Cars);
```

### Basic setup without a dosaic web host (optional)

If you don't use the dosaic webhost,
which automatically configures the DI container,
you'll need to register the S3 plugin manually:

```csharp
services.AddS3BlobStoragePlugin(new S3Configuration
{
    Endpoint = "s3.example.com",
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    Region = "us-west-1", // optional
    UseSsl = true,        // optional
    HealthCheckPath = ""  // optional
});
```

## Working with Files

Example of using the file storage interface:

```csharp
public class FileProvider(IFileStorage fileStorage)
{
    private Task CheckPermissionAsync(FileId fileId, CancellationToken cancellationToken)
    {
        // check permissions or other logic
        if (persmission == null)
            throw Exception("Could not find requested file");
    }
    public async Task<BlobFile> GetFileAsync(FileId id, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        return await fileStorage.GetFileAsync(id, cancellationToken);
    }

    public async Task ConsumeStreamAsync(FileId id, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        await fileStorage.ConsumeStreamAsync(id, streamConsumer, cancellationToken);
    }

    public Task<FileId> SetAsync(BlobFile file, Stream stream, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        return fileStorage.SetAsync(file, stream, cancellationToken);
    }

    public async Task DeleteFileAsync(FileId id, CancellationToken cancellationToken = default)
    {
        await CheckPermissionAsync(id, cancellationToken);
        await fileStorage.DeleteFileAsync(id, cancellationToken);
    }
}

```

## Example usage in a controller

```csharp
[[ApiController, Route("/files"), Authorize]
public class FilesController(IFileStorage fileStorage) : ControllerBase
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
        Response.Headers.Append("Cache-Control", "private, max-age=86400, immutable, must-revalidate");

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



