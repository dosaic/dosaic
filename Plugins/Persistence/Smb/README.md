# Dosaic.Plugins.Persistence.Smb

`Dosaic.Plugins.Persistence.Smb` is a plugin that provides SMB/CIFS (Samba) file storage for Dosaic applications. It exposes an `ISmbStorage` abstraction for reading, writing, deleting, and managing files and folders on SMB-compatible network shares, with support for drive mappings, share mappings, and automatic path resolution.

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.Smb
```

Or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.Smb" Version="" />
```

## Configuration

The plugin binds its configuration from the `smb` section (via `[Configuration("smb")]`).

### appsettings.yml

```yaml
smb:
  server: example.com
  domain: mydomain.com
  username: smbuser
  password: s3cr3t
  defaultShare: myshare
  driveMappings:
    - drive: 'J:'
      share: docs
    - drive: 'K:'
      share: archive
  shareMappings:
    - folder: Reports
      share: reports-share
    - folder: Uploads
      share: uploads-share
```

### Configuration Properties

| Property | Type | Description |
|---|---|---|
| `server` | `string` | Hostname or IP address of the SMB server |
| `domain` | `string` | Windows domain for authentication |
| `username` | `string` | SMB username |
| `password` | `string` | SMB password |
| `defaultShare` | `string` | Fallback share used when no mapping matches |
| `driveMappings` | `DriveMapping[]` | Map Windows drive letters (e.g. `J:`) to share names |
| `shareMappings` | `PathMapping[]` | Map path prefixes (folders) to specific share names |

#### Path resolution order

1. **Drive mappings** — if the path starts with a mapped drive letter (e.g. `J:\Reports\file.pdf`), the drive is replaced with the corresponding share.
2. **Share mappings** — if the path starts with a mapped folder prefix (e.g. `Reports\file.pdf`), that prefix is replaced with the corresponding share.
3. **Default share** — if neither mapping matches and the first path segment cannot be resolved as an existing SMB node, `defaultShare` is used.

## Usage

### Registering the plugin

`SmbStoragePlugin` implements `IPluginServiceConfiguration` and is discovered automatically by the Dosaic source generator. It registers `ISmbStorage` as a singleton in the DI container. Ensure the configuration section is present in your `appsettings.yml`.

### Injecting `ISmbStorage`

```csharp
public class DocumentService(ISmbStorage smbStorage)
{
    private readonly ISmbStorage _smbStorage = smbStorage;
}
```

### Writing files

```csharp
// Write raw bytes
byte[] pdfBytes = File.ReadAllBytes("local.pdf");
await _smbStorage.WriteAsync("Reports/Q1/report.pdf", pdfBytes, cancellationToken);

// Write a UTF-8 string (extension method)
await _smbStorage.WriteStringAsync("Logs/app.log", "Application started", cancellationToken);

// Write a stream (extension method)
await using var fileStream = File.OpenRead("photo.jpg");
await _smbStorage.WriteStreamAsync("Photos/photo.jpg", fileStream, cancellationToken);
```

### Reading files

```csharp
// Read as a Stream
await using var stream = await _smbStorage.ReadStreamAsync("Reports/Q1/report.pdf", cancellationToken);

// Read as a string (extension method)
string logContent = await _smbStorage.ReadStringAsync("Logs/app.log", cancellationToken);

// Read as a byte array (extension method)
byte[] data = await _smbStorage.ReadBytesAsync("Reports/Q1/report.pdf", cancellationToken);
```

### Ensuring a directory path exists

`EnsurePathAsync` traverses each segment of the path and creates any missing folders recursively.

```csharp
await _smbStorage.EnsurePathAsync("Reports/2025/Q1/", cancellationToken);
```

### Deleting files

`DeleteIfExists` silently ignores the case where the file does not exist. It throws a `DosaicException` if the file is found but cannot be deleted.

```csharp
await _smbStorage.DeleteIfExists("Reports/Q1/old-report.pdf", cancellationToken);
```

### Using drive and share mappings

```csharp
// With a drive mapping: J: -> docs-share
await _smbStorage.WriteAsync(@"J:\Invoices\2025\inv001.pdf", data, cancellationToken);
// Resolves to: \\example.com\docs-share\Invoices\2025\inv001.pdf

// With a share mapping: Uploads -> uploads-share
await _smbStorage.WriteAsync("Uploads/avatars/user42.png", data, cancellationToken);
// Resolves to: \\example.com\uploads-share\avatars\user42.png
```

## Features

- **`ISmbStorage` abstraction** — decouples application code from the concrete SMB implementation; easy to mock in tests
- **Flexible path resolution** — drive mappings, share mappings, and a configurable default share with automatic fallback
- **Slash normalisation** — forward slashes (`/`) and backslashes (`\`) are interchangeable in all path arguments
- **`EnsurePathAsync`** — recursively creates missing folders on the SMB share
- **`DeleteIfExists`** — idempotent deletion; no error when the target is already absent
- **Extension methods** — `WriteStringAsync`, `WriteStreamAsync`, `ReadStringAsync`, `ReadBytesAsync` available on any `ISmbStorage` instance
- **OpenTelemetry metrics** — built-in counters for observability:
  - `dosaic_persistence_smb_writes_total`
  - `dosaic_persistence_smb_reads_total`
  - `dosaic_persistence_smb_deletes_total`
- **Powered by [EzSmb](https://github.com/smbkit/EzSmb)** — pure-managed SMB client requiring no OS-level mounts
