# Dosaic.Extensions.RestEase

Dosaic.Extensions.RestEase is an extension library that simplifies HTTP API client creation using RestEase with built-in authentication and retry policies.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Extensions.RestEase
```

or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Extensions.RestEase" Version="" />
```

## Usage

The extension provides a factory for creating typed API clients with minimal configuration.

### Basic Client Creation

Create a simple API client interface:

```csharp
using RestEase;

public interface IMyApi
{
    [Get("users/{userId}")]
    Task<User> GetUserAsync([Path] string userId);

    [Post("users")]
    Task<User> CreateUserAsync([Body] User user);
}
```

Then create a client instance:

```csharp
using Dosaic.Extensions.RestEase;

IMyApi apiClient = RestClientFactory.Create<IMyApi>("https://api.example.com");
User user = await apiClient.GetUserAsync("123");
```

### Authentication Support

Create a client with OAuth2 authentication:

```csharp
using Dosaic.Extensions.RestEase;
using Dosaic.Extensions.RestEase.Authentication;

var authConfig = new AuthenticationConfig
{
    Enabled = true,
    BaseUrl = "https://auth.example.com",
    TokenUrlPath = "oauth/token",
    GrantType = GrantType.ClientCredentials,
    ClientId = "myclientid",
    ClientSecret = "myclientsecret"
};

IMyApi apiClient = RestClientFactory.Create<IMyApi>("https://api.example.com", authConfig);
```

### Custom Retry Policy

You can specify a custom Polly retry policy:

```csharp
using Dosaic.Extensions.RestEase;
using Polly;
using System.Net.Http;

var retryPolicy = Policy
    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));

IMyApi apiClient = RestClientFactory.Create<IMyApi>("https://api.example.com", retryPolicy);
```

### Advanced Configuration

For complete customization, use all parameters:

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
    GrantType = GrantType.Password,
    Username = "myusername",
    Password = "mypassword"
};

var retryPolicy = Policy
    .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
    .RetryAsync(3);

var jsonSettings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore
};

IMyApi apiClient = RestClientFactory.Create<IMyApi>(
    "https://api.example.com",
    authConfig,
    retryPolicy,
    jsonSettings
);
```

## Authentication Types

The extension supports these OAuth2 grant types:

- Password - username and password authentication
- ClientCredentials - client ID and secret authentication
- Code - authorization code flow

```csharp
// Password grant
var passwordAuth = new AuthenticationConfig
{
    Enabled = true,
    GrantType = GrantType.Password,
    Username = "user",
    Password = "pass"
};

// Client credentials
var clientAuth = new AuthenticationConfig
{
    Enabled = true,
    GrantType = GrantType.ClientCredentials,
    ClientId = "client123",
    ClientSecret = "secret456"
};
```

## Default Behavior

By default, the extension uses:

- JSON serialization with string enum conversion
- A retry policy that retries 2 times for HTTP 5xx errors
- Automatic token refresh for authenticated requests
