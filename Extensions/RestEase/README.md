# Dosaic.Extensions.RestEase

`Dosaic.Extensions.RestEase` is an extension library that simplifies typed HTTP API client creation using [RestEase](https://github.com/canton7/RestEase). It provides a static `RestClientFactory` with built-in OAuth2 authentication and Polly-based retry policies out of the box.

## Installation

```shell
dotnet add package Dosaic.Extensions.RestEase
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Extensions.RestEase" Version="" />
```

## Features

- **Typed HTTP clients** — define an interface with RestEase attributes and get a fully functional client
- **OAuth2 authentication** — automatic token acquisition (password, client credentials, authorization code grants)
- **Automatic token refresh** — transparently refreshes the access token using the refresh token before it expires
- **Polly retry policy** — configurable retry; defaults to 2 retries (3 total attempts) on HTTP 5xx responses
- **Custom JSON serialisation** — defaults to Newtonsoft.Json with `StringEnumConverter`; fully replaceable
- **All overloads composable** — use only the features you need; everything beyond `baseAddress` is optional

## Configuration

`AuthenticationConfig` can be populated from `appsettings.json` / `appsettings.yaml` via the standard `IConfiguration` binding:

```json
{
  "MyApi": {
    "Auth": {
      "Enabled": true,
      "BaseUrl": "https://auth.example.com",
      "TokenUrlPath": "realms/myrealm/protocol/openid-connect/token",
      "GrantType": "ClientCredentials",
      "ClientId": "my-client",
      "ClientSecret": "s3cr3t"
    }
  }
}
```

```yaml
MyApi:
  Auth:
    Enabled: true
    BaseUrl: https://auth.example.com
    TokenUrlPath: realms/myrealm/protocol/openid-connect/token
    GrantType: ClientCredentials
    ClientId: my-client
    ClientSecret: s3cr3t
```

Bind to `AuthenticationConfig` in your plugin or startup code:

```csharp
var authConfig = configuration.GetSection("MyApi:Auth").Get<AuthenticationConfig>();
```

## Usage

### Basic Client

Define an interface using RestEase attributes:

```csharp
using RestEase;

public interface IUserApi
{
    [Get("users/{userId}")]
    Task<User> GetUserAsync([Path] Guid userId, CancellationToken cancellationToken);

    [Post("users")]
    Task<User> CreateUserAsync([Body] User user, CancellationToken cancellationToken);

    [Put("users/{userId}")]
    Task UpdateUserAsync([Path] Guid userId, [Body] User user, CancellationToken cancellationToken);

    [Delete("users/{userId}")]
    Task DeleteUserAsync([Path] Guid userId, CancellationToken cancellationToken);
}
```

Create a client instance with just a base address:

```csharp
using Dosaic.Extensions.RestEase;

IUserApi api = RestClientFactory.Create<IUserApi>("https://api.example.com");
var user = await api.GetUserAsync(userId, CancellationToken.None);
```

### Authentication

Pass an `AuthenticationConfig` to enable OAuth2. The `AuthHandler` acquires a token on the first request and automatically refreshes it when the access token expires (as long as the refresh token is still valid):

```csharp
using Dosaic.Extensions.RestEase;
using Dosaic.Extensions.RestEase.Authentication;

var authConfig = new AuthenticationConfig
{
    Enabled = true,
    BaseUrl = "https://auth.example.com",
    TokenUrlPath = "realms/myrealm/protocol/openid-connect/token",
    GrantType = GrantType.ClientCredentials,
    ClientId = "my-client",
    ClientSecret = "s3cr3t"
};

IUserApi api = RestClientFactory.Create<IUserApi>("https://api.example.com", authConfig);
```

#### Supported Grant Types

| `GrantType` | OAuth2 `grant_type` value | Required fields |
|---|---|---|
| `ClientCredentials` | `client_credentials` | `ClientId`, `ClientSecret` |
| `Password` | `password` | `ClientId`, `Username`, `Password` |
| `Code` | `code` | `ClientId`, `ClientSecret` |

```csharp
// Password grant
var authConfig = new AuthenticationConfig
{
    Enabled = true,
    BaseUrl = "https://auth.example.com",
    TokenUrlPath = "oauth/token",
    GrantType = GrantType.Password,
    ClientId = "my-client",
    Username = "alice",
    Password = "s3cr3t"
};
```

### Custom Retry Policy

Supply any `IAsyncPolicy<HttpResponseMessage>` from Polly:

```csharp
using Dosaic.Extensions.RestEase;
using Polly;
using System.Net.Http;

var retryPolicy = Policy
    .HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.Conflict)
    .RetryAsync(2);

IUserApi api = RestClientFactory.Create<IUserApi>("https://api.example.com", retryPolicy);
```

### Advanced — All Options

```csharp
using Dosaic.Extensions.RestEase;
using Dosaic.Extensions.RestEase.Authentication;
using Newtonsoft.Json;
using Polly;

var authConfig = new AuthenticationConfig
{
    Enabled = true,
    BaseUrl = "https://auth.example.com",
    TokenUrlPath = "oauth/token",
    GrantType = GrantType.ClientCredentials,
    ClientId = "my-client",
    ClientSecret = "s3cr3t"
};

var retryPolicy = Policy
    .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
    .RetryAsync(3);

var jsonSettings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore
};

IUserApi api = RestClientFactory.Create<IUserApi>(
    "https://api.example.com",
    authConfig,
    retryPolicy,
    jsonSettings
);
```

## API Reference

### `RestClientFactory`

| Method | Description |
|---|---|
| `Create<T>(string baseAddress)` | Creates a client with default retry and no auth |
| `Create<T>(string baseAddress, AuthenticationConfig auth)` | Adds OAuth2 authentication |
| `Create<T>(string baseAddress, IAsyncPolicy<HttpResponseMessage> policy)` | Replaces the default retry policy |
| `Create<T>(string baseAddress, AuthenticationConfig auth, IAsyncPolicy<HttpResponseMessage> policy)` | Auth + custom retry |
| `Create<T>(string baseAddress, AuthenticationConfig auth, IAsyncPolicy<HttpResponseMessage> policy, JsonSerializerSettings json)` | Full control |
| `DefaultJsonSerializerSettings` | Static default — Newtonsoft.Json with `StringEnumConverter` |

### `AuthenticationConfig`

| Property | Type | Description |
|---|---|---|
| `Enabled` | `bool` | Enables OAuth2 token injection |
| `BaseUrl` | `string` | Base URL of the OAuth2 token endpoint |
| `TokenUrlPath` | `string` | Path to the token endpoint (appended to `BaseUrl`) |
| `GrantType` | `GrantType` | OAuth2 grant type |
| `ClientId` | `string` | OAuth2 client identifier |
| `ClientSecret` | `string` | OAuth2 client secret |
| `Username` | `string` | Resource owner username (password grant) |
| `Password` | `string` | Resource owner password (password grant) |

## Default Behaviour

- **JSON** — Newtonsoft.Json with `StringEnumConverter` (enums serialised as strings)
- **Retry** — `RetryAsync(2)` on HTTP 5xx (500–599), meaning 3 total attempts
- **Auth** — disabled by default; no `Authorization` header is added unless `AuthenticationConfig.Enabled = true`
- **Token refresh** — when the access token is expired but the refresh token is still valid, the library uses the `refresh_token` grant automatically; when both are expired a new token is acquired
