# Dosaic.Plugins.Authorization.Zitadel

`Dosaic.Plugins.Authorization.Zitadel` is a plugin that provides OAuth2 token-introspection authentication against a [Zitadel](https://zitadel.com/) server. It registers the official `Zitadel` authentication scheme, validates Bearer tokens via the Zitadel introspection endpoint (using a JWT application profile), and exposes a management service for programmatically creating and managing service accounts and user accounts.

## Features

- **OAuth2 token introspection** — validates Bearer tokens through Zitadel's introspection endpoint using an `Application` JWT profile; no manual key fetching required
- **Built-in response caching** — introspection results are cached in the distributed cache to reduce round-trips; cache duration and key prefix are configurable
- **Discovery policy control** — configure whether HTTPS is required for the discovery endpoint, and whether issuer/endpoint validation is enforced
- **`IManagementService`** — injectable service to list service accounts, create service accounts (with JWT keys), create human user accounts, and obtain Bearer tokens for service accounts
- **Authentication failure logging** — all authentication failures are logged automatically via the plugin's `ILogger`

## Installation

```shell
dotnet add package Dosaic.Plugins.Authorization.Zitadel
```

Or add the package reference directly to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Authorization.Zitadel" Version="" />
```

## Configuration

The plugin is configured under the `Zitadel` key via the `[Configuration("Zitadel")]` attribute on `ZitadelConfiguration`.

### Configuration class

```csharp
[Configuration("Zitadel")]
public class ZitadelConfiguration
{
    // Zitadel project ID (used to build the audience scope for service account tokens)
    public required string ProjectId { get; set; }

    // Zitadel organization ID (used when creating users/service accounts)
    public required string OrganizationId { get; set; }

    // Base URL of the Zitadel instance, e.g. "https://my-org.zitadel.cloud"
    public required string Host { get; set; }

    // When true (default), the authority URL uses https://; set to false for local/dev environments
    public bool UseHttps { get; set; } = true;

    // Whether to validate the issuer during OIDC discovery (default: true)
    public bool ValidateIssuer { get; set; } = true;

    // Whether to validate discovery endpoints (default: true)
    public bool ValidateEndpoints { get; set; } = true;

    // Enable caching of introspection results (default: true)
    public bool EnableCaching { get; set; } = true;

    // How long introspection results are cached (default: 1 minute)
    public int CacheDurationInMinutes { get; set; } = 1;

    // Prefix for cache keys (default: "ZITADEL_")
    public string CacheKeyPrefix { get; set; } = "ZITADEL_";

    // JSON string of the Zitadel Application JWT profile used for introspection
    public required string JwtProfile { get; set; }

    // JSON string of the Zitadel Service Account JWT profile used by IManagementService
    public string ServiceAccount { get; set; }
}
```

> **Note:** Passing an `http://` host while `UseHttps = true` will throw an `InvalidConfigurationException` at startup.

### `appsettings.yml` example

```yaml
Zitadel:
  projectId: "23456789012345678"
  organizationId: "123456789012345678"
  host: https://my-org.zitadel.cloud
  useHttps: true
  validateIssuer: true
  validateEndpoints: true
  enableCaching: true
  cacheDurationInMinutes: 1
  cacheKeyPrefix: "ZITADEL_"
  jwtProfile: |
    {
      "type": "application",
      "keyId": "...",
      "key": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----\n",
      "appId": "...",
      "clientId": "..."
    }
  serviceAccount: |
    {
      "type": "serviceaccount",
      "keyId": "...",
      "key": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----\n",
      "userId": "..."
    }
```

### Disable HTTPS for local development

```yaml
Zitadel:
  host: http://localhost:8080
  useHttps: false
  validateIssuer: false
  validateEndpoints: false
  projectId: "1"
  organizationId: "1"
  jwtProfile: "{}"
  serviceAccount: "{}"
```

When `useHttps` is `false`, the plugin uses `http://` for the authority URL and relaxes discovery validation — useful when running Zitadel locally via Docker.

## Usage

### Minimal API endpoints

```csharp
public class MyEndpoints : IPluginEndpointsConfiguration
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
    {
        endpointRouteBuilder.MapGet("/hello", () => "Hello World!")
            .RequireAuthorization();

        endpointRouteBuilder.MapGet("/admin", () => "Admin area")
            .RequireAuthorization(policy => policy.RequireRole("admin"));
    }
}
```

### MVC controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult GetAll() => Ok();

    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public() => Ok("no auth needed");
}
```

### How token validation works

1. The `Authorization: Bearer <token>` header is extracted from the incoming request.
2. The token is sent to Zitadel's introspection endpoint (`<host>/oauth/v2/introspect`) together with the Application JWT profile loaded from `JwtProfile`.
3. Zitadel returns the token metadata (active, subject, scopes, roles, etc.) which is mapped to the `ClaimsPrincipal`.
4. If `EnableCaching = true`, the introspection result is stored in the distributed cache under `<CacheKeyPrefix><token-hash>` for `CacheDurationInMinutes` to avoid repeated network calls.
5. On any authentication failure the error is logged at `Error` level by the plugin's `ILogger<ZitadelPlugin>`.

## Management service

`IManagementService` is registered automatically and can be injected to manage Zitadel users programmatically.

```csharp
public interface IManagementService
{
    // List all machine (service account) users in the configured organization
    Task<List<ListServiceAccountResultItem>> ListServiceAccountsAsync();

    // Create a new service account user and return its JWT key content
    Task<ServiceAccountCreationResult> CreateServiceAccountAsync(
        string userid, string name, Timestamp keyExpirationTime, string description = "");

    // Obtain a Bearer token for a service account identified by its JSON key string
    Task<string> GetBearerTokenForServiceAccountAsync(string jsonString);

    // Create a human user account with an initial password
    Task<string> CreateUserAccountAsync(
        string userid, string email,
        string displayName, string givenName, string familyName, string password);
}

public record ServiceAccountCreationResult(string UserId, string KeyId, string KeyContent);

public record ListServiceAccountResultItem(
    string UserId, string State, string Name, string Description,
    bool HasSecret, string Owner, DateTime CreatedAt, DateTime UpdatedAt);
```

### Example — list service accounts

```csharp
public class MyService(IManagementService management)
{
    public async Task PrintServiceAccounts()
    {
        var accounts = await management.ListServiceAccountsAsync();
        foreach (var account in accounts)
            Console.WriteLine($"{account.UserId} — {account.Name} ({account.State})");
    }
}
```

### Example — create a service account

```csharp
var expirationTime = Timestamp.FromDateTime(DateTime.UtcNow.AddYears(1));
var result = await management.CreateServiceAccountAsync(
    userid: "my-service-id",
    name: "my-service",
    keyExpirationTime: expirationTime,
    description: "Service account for my-service");

Console.WriteLine($"UserId: {result.UserId}");
Console.WriteLine($"Key JSON: {result.KeyContent}");
```

## Calling an API protected by Zitadel from a service account client

Use `GetBearerTokenForServiceAccountAsync` (or call the Zitadel SDK directly) to obtain a Bearer token for a service account and attach it to outgoing HTTP requests.

```csharp
// Using IManagementService (reads service account JSON from configuration)
var token = await managementService.GetBearerTokenForServiceAccountAsync(jwtProfileJsonString);

var client = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Get, "http://<api-url>/api/endpoint");
request.Headers.Add("Authorization", "Bearer " + token);
var response = await client.SendAsync(request);
```

Or call the Zitadel SDK directly when you need additional scopes:

```csharp
var authOptions = new ServiceAccount.AuthOptions();
// be sure to include this scope or your introspection will not be able to verify token for the indented project
authOptions.AdditionalScopes.Add("urn:zitadel:iam:org:project:id:123456789012345678:aud");

authOptions.AdditionalScopes.Add("offline_access");
authOptions.AdditionalScopes.Add("profile");
authOptions.AdditionalScopes.Add("email");

var token = await ServiceAccount.LoadFromJsonString("<jwtProfileJsonString>")
    .AuthenticateAsync("<HostUrl>", authOptions);

var client = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Get, "http://<api-url>/api/endpoint");
request.Headers.Add("Authorization", "Bearer " + token);
var response = await client.SendAsync(request);
```

The `urn:zitadel:iam:org:project:id:<projectId>:aud` scope tells Zitadel to include your project's audience in the token so the receiving API's introspection call succeeds.

