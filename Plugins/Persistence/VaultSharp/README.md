# Dosaic.Plugins.Persistence.VaultSharp

`Dosaic.Plugins.Persistence.VaultSharp` is a Dosaic plugin that integrates [HashiCorp Vault](https://www.vaulthuashicorp.io/) for secure secret storage. It provides a typed `ISecretStorage<TBucket>` abstraction backed either by a live Vault server (KV v2 + TOTP engines) or a local filesystem (useful for development and testing without a running Vault instance).

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.VaultSharp
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.VaultSharp" Version=""/>
```

## Configuration

The plugin is configured via the `vault` section in `appsettings.yml` / `appsettings.json`. The section is bound to `VaultConfiguration` using the `[Configuration("vault")]` attribute.

### Vault server mode

```yaml
vault:
  url: "http://localhost:8200"
  token: "your-vault-token"
  totpIssuer: "MyApp"               # optional — default: Dosaic.Plugins.Persistence.VaultSharp
  totpPeriodInSeconds: 30            # optional — default: 30
```

### Local filesystem mode (development / testing)

When `useLocalFileSystem` is `true`, secrets are stored as JSON files under `localFileSystemPath`. No Vault server is required. TOTP codes are generated locally using RFC 6238 (HMAC-SHA1).

```yaml
vault:
  useLocalFileSystem: true
  localFileSystemPath: "./nodep-vault"   # optional — default: ./nodep-vault
  totpPeriodInSeconds: 30                # optional — default: 30
```

### VaultConfiguration properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Url` | `string` | `""` | Vault server base URL |
| `Token` | `string` | `""` | Token used to authenticate with Vault |
| `TotpIssuer` | `string` | `"Dosaic.Plugins.Persistence.VaultSharp"` | Issuer label stored in the TOTP key |
| `TotpPeriodInSeconds` | `int` | `30` | TOTP counter period in seconds |
| `UseLocalFileSystem` | `bool` | `false` | Store secrets on the local filesystem instead of Vault |
| `LocalFileSystemPath` | `string` | `"./nodep-vault"` | Root folder for local filesystem secrets |

## Vault mount points

The plugin uses two fixed Vault secret engine mount points:

| Engine | Mount |
|---|---|
| KV v2 | `credentials` |
| TOTP | `totp` |

Both engines must exist in Vault before the application starts.

## Registration

### With Dosaic WebHost (automatic)

When using `PluginWebHostBuilder`, the plugin is discovered automatically and `VaultConfiguration` is resolved from `IConfiguration`. You only need to register your secret storage buckets — typically in your own plugin or startup code:

```csharp
services.AddSecretStorage<SecretBucket>();
```

### Without Dosaic WebHost (manual)

```csharp
services.AddVaultSharpPlugin(new VaultConfiguration
{
    Url = "http://localhost:8200",
    Token = "your-vault-token"
});

services.AddSecretStorage<SecretBucket>();
```

For local filesystem storage without a Vault server:

```csharp
var config = new VaultConfiguration { UseLocalFileSystem = true, LocalFileSystemPath = "./nodep-vault" };
services.AddLocalFileSystemSecretStorage<SecretBucket>(config);
```

## Usage

### Defining a secret bucket

Buckets are plain enums that categorise secrets stored in Vault:

```csharp
public enum SecretBucket
{
    ApiKeys = 0,
    DatabaseCredentials = 1,
    ServiceAccounts = 2
}
```

### ISecretStorage\<TBucket\> interface

```csharp
public interface ISecretStorage<TSecretBucket> where TSecretBucket : struct, Enum
{
    Task<Secret> GetSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default);
    Task<SecretId<TSecretBucket>> CreateSecretAsync(TSecretBucket bucket, Secret secret, CancellationToken cancellationToken = default);
    Task<SecretId<TSecretBucket>> UpdateSecretAsync(SecretId<TSecretBucket> secretId, Secret secret, CancellationToken cancellationToken = default);
    Task DeleteSecretAsync(SecretId<TSecretBucket> secretId, CancellationToken cancellationToken = default);
}
```

Inject it via the constructor:

```csharp
public class MyService(ISecretStorage<SecretBucket> secrets)
{
    // ...
}
```

### SecretId\<TBucket\>

`SecretId<TBucket>` is a read-only struct that identifies a stored secret. It encodes the bucket, type and a unique key into a URL-safe [Sqid](https://sqids.org/) string via the `Id` property.

```csharp
// Create a new random ID for a bucket + type combination
var id = SecretId<SecretBucket>.New(SecretBucket.DatabaseCredentials, SecretType.UsernamePassword);

// Encode / decode the opaque string representation
string opaqueId = id.Id;
var same = SecretId<SecretBucket>.FromSqid(opaqueId);

// Safe parsing (e.g. from user input)
if (SecretId<SecretBucket>.TryParse(input, out var parsed))
    Console.WriteLine(parsed.Bucket); // DatabaseCredentials
```

### Secret types

All secret types derive from the abstract record `Secret`.

#### UsernamePasswordSecret

```csharp
var secret = new UsernamePasswordSecret("admin", "s3cr3t");

var id = await secrets.CreateSecretAsync(SecretBucket.DatabaseCredentials, secret);
var retrieved = (UsernamePasswordSecret)await secrets.GetSecretAsync(id);
Console.WriteLine(retrieved.Username); // admin
```

#### UsernamePasswordApiKeySecret

Extends `UsernamePasswordSecret` with an additional `ApiKey` field.

```csharp
var secret = new UsernamePasswordApiKeySecret("svc-account", "p@ssw0rd", "api-key-abc123");

var id = await secrets.CreateSecretAsync(SecretBucket.ApiKeys, secret);
var retrieved = (UsernamePasswordApiKeySecret)await secrets.GetSecretAsync(id);
Console.WriteLine(retrieved.ApiKey); // api-key-abc123
```

#### UsernamePasswordTotpSecret

Stores a username, password, and a TOTP key. On **create / update** the `Totp` must contain a `TotpKey` with the Base32-encoded seed. On **read** the `Totp` is populated with a `TotpCode` containing the current OTP, its expiry time and remaining seconds.

```csharp
// Write — provide the Base32 seed key
var secret = new UsernamePasswordTotpSecret(
    "admin",
    "p@ssw0rd",
    new Totp(null, new TotpKey("JBSWY3DPEHPK3PXP"))
);
var id = await secrets.CreateSecretAsync(SecretBucket.ServiceAccounts, secret);

// Read — the live TOTP code is resolved automatically
var retrieved = (UsernamePasswordTotpSecret)await secrets.GetSecretAsync(id);
Console.WriteLine(retrieved.Totp.TotpCode.Code);             // e.g. "482910"
Console.WriteLine(retrieved.Totp.TotpCode.RemainingSeconds); // seconds until code expires
Console.WriteLine(retrieved.Totp.TotpCode.ValidTillUtc);     // UTC expiry timestamp
```

#### CertificateSecret

```csharp
var secret = new CertificateSecret("-----BEGIN CERTIFICATE-----\n...", "optional-passphrase");

var id = await secrets.CreateSecretAsync(SecretBucket.ServiceAccounts, secret);
var retrieved = (CertificateSecret)await secrets.GetSecretAsync(id);
Console.WriteLine(retrieved.Certificate);
```

### Updating and deleting secrets

```csharp
// Update in-place — the SecretId stays the same
var updated = new UsernamePasswordSecret("admin", "new-password");
await secrets.UpdateSecretAsync(id, updated);

// Delete
await secrets.DeleteSecretAsync(id);
```

### Local TOTP code generation (TotpCodeGenerator)

`TotpCodeGenerator` is a static helper that generates TOTP codes locally (RFC 6238, HMAC-SHA1) without calling Vault. It is used internally by `LocalFileSystemSecretStorage` but is also available for direct use:

```csharp
// Generate a 6-digit TOTP code from a Base32 key
string code = TotpCodeGenerator.Generate("JBSWY3DPEHPK3PXP", periodInSeconds: 30);

// Get remaining seconds and expiry for the current period
var (remaining, validUntil) = TotpCodeGenerator.GetPeriodInfo(periodInSeconds: 30);
```

## Features

- **Typed secret buckets** — secrets are namespaced by a user-defined enum; different bucket enums compile to different `ISecretStorage<T>` registrations
- **Four secret types** — `UsernamePassword`, `UsernamePasswordApiKey`, `UsernamePasswordTotp`, `Certificate`
- **Opaque secret IDs** — bucket, type and key are encoded into a URL-safe Sqid string for safe external exposure
- **First-class TOTP support** — creates/manages TOTP keys in Vault's TOTP engine; decodes live OTP codes on every read
- **Local filesystem backend** — drop-in replacement for local development and testing; TOTP codes generated locally via RFC 6238
- **OpenTelemetry tracing** — all storage operations are wrapped in `ActivitySource` spans with `secret.bucket`, `secret.key`, and `secret.type` tags
- **Readiness health check** — `vault` (Vault HTTP health endpoint) or `vault-local-filesystem` (directory write probe) registered automatically as a readiness check
- **Dosaic WebHost integration** — discovered and wired automatically by the source generator; no manual bootstrap required when using `PluginWebHostBuilder`

## Health Checks

The plugin registers a **readiness** health check automatically:

| Mode | Check name | Probe |
|---|---|---|
| Vault server | `vault` | `GET /v1/sys/health` via `IVaultClient` |
| Local filesystem | `vault-local-filesystem` | Creates and deletes a `.health-probe` file in `LocalFileSystemPath` |

Both checks report `Unhealthy` on failure, preventing the application from receiving traffic until the backend is reachable.
