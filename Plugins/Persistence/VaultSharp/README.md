# Dosaic.Plugins.Persistence.VaultSharp

Dosaic.Plugins.Persistence.VaultSharp is a plugin that allows other Dosaic components to interact with HashiCorp Vault for secure secret storage.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Persistence.VaultSharp
```

or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.VaultSharp" Version=""/>
```

## Appsettings.yml

Configure your appsettings.yml with these properties:

```yaml
vault:
  url: "http://localhost:8200"
  token: "your-vault-token"
  totpIssuer: "myissuer" # optional, default is Dosaic.Plugins.Persistence.VaultSharp
  totpPeriodInSeconds: 30 # optional, default is 30 seconds
```

## Registration and Configuration

### Secret Storage

To use the secret storage functionality, first define an enum for your secret buckets e.g.:

```csharp
public enum SecretBucket
{
    ApiKeys = 0,
    DatabaseCredentials = 1,
    UserSecrets = 2
}
```

Then register the secret storage for your bucket enum:

```csharp
services.AddSecretStorage<SecretBucket>();
```

This registers `ISecretStorage<SecretBucket>` which can be injected into your services.

### Basic setup without a dosaic web host (optional)

If you don't use the dosaic webhost, which automatically configures the DI container, you'll need to register the VaultSharp plugin manually:

```csharp
services.AddVaultSharpPlugin(new VaultConfiguration
{
    Url = "http://localhost:8200",
    Token = "your-vault-token"
    TotpIssuer = "myissuer", // optional, default is Dosaic.Plugins.Persistence.VaultSharp
    TotpPeriodInSeconds = 30 // optional, default is 30 seconds
});
```

## Working with Secrets

Example of using the secret storage interface:

```csharp
public class SecretProvider(ISecretStorage<SecretBucket> secretStorage)
{
    public async Task<Secret> GetSecretAsync(SecretId<SecretBucket> secretId, CancellationToken cancellationToken = default)
    {
        return await secretStorage.GetSecretAsync(secretId, cancellationToken);
    }

    public async Task<SecretId<SecretBucket>> CreateSecretAsync(SecretBucket bucket, Secret secret, CancellationToken cancellationToken = default)
    {
        return await secretStorage.CreateSecretAsync(bucket, secret, cancellationToken);
    }

    public async Task<SecretId<SecretBucket>> UpdateSecretAsync(SecretId<SecretBucket> secretId, Secret secret, CancellationToken cancellationToken = default)
    {
        return await secretStorage.UpdateSecretAsync(secretId, secret, cancellationToken);
    }

    public async Task DeleteSecretAsync(SecretId<SecretBucket> secretId, CancellationToken cancellationToken = default)
    {
        await secretStorage.DeleteSecretAsync(secretId, cancellationToken);
    }
}
```

## Health Checks

The VaultSharp plugin automatically configures a readiness health check that verifies connectivity with the Vault server. This ensures your application doesn't start until it can securely access secrets.
