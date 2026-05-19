# Dosaic.Extensions.RestEase

`Dosaic.Extensions.RestEase` builds typed HTTP API clients on top of [RestEase](https://github.com/canton7/RestEase). It plugs into `IHttpClientFactory`, uses **System.Text.Json**, supports a composable **DelegatingHandler middleware chain**, **Polly v8 resilience pipelines** (via `Microsoft.Extensions.Http.Resilience`), and ships an **OAuth2** integration with thread-safe token caching, automatic refresh-token rotation, and 401-triggered force-refresh retry.

## Installation

```shell
dotnet add package Dosaic.Extensions.RestEase
```

```xml
<PackageReference Include="Dosaic.Extensions.RestEase" Version="" />
```

## Features

- **Typed HTTP clients** — define an interface with RestEase attributes, get a fully wired client back.
- **IHttpClientFactory integration** — proper socket pooling, DNS refresh, named-client lifetime management.
- **DelegatingHandler middleware chain** — plug in correlation IDs, logging, custom auth, rate-limit headers, request signing, etc.
- **Polly v8 resilience pipelines** — retry with jitter + exponential backoff, `Retry-After` honouring, timeouts, circuit breaker, hedging, bulkhead. Powered by `Microsoft.Extensions.Http.Resilience`.
- **OAuth2** out of the box — `ClientCredentials` and `Password` grants; transparent refresh-token rotation; concurrent-call coalescing via `SemaphoreSlim`; 401-triggered forced refresh + retry.
- **Pluggable `ITokenProvider`** — swap in a distributed token cache, mTLS, API key, or any custom auth strategy.
- **System.Text.Json only** — override `JsonSerializerOptions` per-client; sane web defaults out of the box.
- **DI builder fluent API** — `AddDosaicRestClient<TApi>()` returns `IRestEaseClientBuilder` for composition.
- **Static factory for non-DI usage** — `RestClientFactory.Create<TApi>(...)` still available.

## Quick Start (DI — recommended)

```csharp
using Dosaic.Extensions.RestEase.DependencyInjection;

services.AddDosaicRestClient<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddStandardResilience();
```

Resolve the typed client:

```csharp
public class UserService(IUserApi api)
{
    public Task<User> Get(Guid id, CancellationToken ct) => api.GetUserAsync(id, ct);
}
```

## Interface Definition

```csharp
using RestEase;

public interface IUserApi
{
    [Get("users/{id}")]
    Task<User> GetUserAsync([Path] Guid id, CancellationToken ct);

    [Post("users")]
    Task<User> CreateAsync([Body] User user, CancellationToken ct);

    [Put("users/{id}")]
    Task UpdateAsync([Path] Guid id, [Body] User user, CancellationToken ct);

    [Delete("users/{id}")]
    Task DeleteAsync([Path] Guid id, CancellationToken ct);
}
```

## Configuration Binding

```yaml
MyApi:
  BaseAddress: https://api.example.com
  Timeout: 00:00:30
  UserAgent: my-service/1.0
  Authentication:
    Enabled: true
    BaseUrl: https://auth.example.com
    TokenUrlPath: /realms/my-realm/protocol/openid-connect/token
    GrantType: ClientCredentials
    ClientId: my-client
    ClientSecret: s3cr3t
    Scope: api.read api.write
    RefreshSkew: 00:00:30
```

```csharp
services.AddDosaicRestClient<IUserApi>(o =>
{
    configuration.GetSection("MyApi").Bind(o);
})
.AddStandardResilience();
```

## DI Builder API

`AddDosaicRestClient<TApi>()` returns an `IRestEaseClientBuilder`:

| Method | Purpose |
|---|---|
| `.ConfigureOptions(Action<RestEaseClientOptions>)` | Mutate client options |
| `.ConfigureJson(Action<JsonSerializerOptions>)` | Tweak the System.Text.Json options |
| `.ConfigureHttpClient(Action<HttpClient>)` | Raw `HttpClient` configuration |
| `.AddOAuth2(Action<AuthenticationConfig>)` | Enable the built-in OAuth2 token provider |
| `.AddTokenProvider<T>()` | Plug in a custom `ITokenProvider` (registered in DI) |
| `.AddStandardResilience()` | `Microsoft.Extensions.Http.Resilience` standard pipeline |
| `.AddResilience(ResiliencePipeline<HttpResponseMessage>)` | Custom Polly v8 pipeline |
| `.AddHandler<THandler>()` | Insert a `DelegatingHandler` into the chain |

## OAuth2

### Client Credentials

```csharp
services.AddDosaicRestClient<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddOAuth2(a =>
        {
            a.BaseUrl = "https://auth.example.com";
            a.TokenUrlPath = "/oauth/token";
            a.GrantType = GrantType.ClientCredentials;
            a.ClientId = "my-client";
            a.ClientSecret = "s3cr3t";
            a.Scope = "api.read";
        })
        .AddStandardResilience();
```

### Resource Owner Password

```csharp
.AddOAuth2(a =>
{
    a.BaseUrl = "https://auth.example.com";
    a.TokenUrlPath = "/oauth/token";
    a.GrantType = GrantType.Password;
    a.ClientId = "my-client";
    a.Username = "alice";
    a.Password = "s3cr3t";
});
```

> **Authorization Code grant is not supported.** The browser leg (user redirect + consent) is out of scope for a server-side HTTP client. Use ASP.NET OIDC (`AddOpenIdConnect`) to handle the interactive login, then plug a custom `ITokenProvider` that reads `HttpContext.GetTokenAsync("access_token")` and feeds it into this client.

### How the OAuth2 pipeline behaves

- **Concurrent-call coalescing.** Multiple requests during a token refresh hit the IdP **once** — gated by `SemaphoreSlim` with double-checked locking.
- **Refresh-token rotation.** When the access token expires but the refresh token is still valid, a `refresh_token` grant is used automatically.
- **Clock-skew buffer.** `AuthenticationConfig.RefreshSkew` (default 30s) refreshes the token slightly before its real expiry.
- **401 → force-refresh + retry.** If the server returns 401 with our auto-injected token, the cached token is invalidated, a fresh one is fetched, and the request is retried once.
- **User-supplied `Authorization` header is respected** — the handler never overwrites a header the caller already set.

### Custom `ITokenProvider`

For distributed token caches, mTLS, API key, or anything else:

```csharp
public sealed class RedisTokenProvider(IConnectionMultiplexer redis) : ITokenProvider
{
    public async Task<AccessToken> GetTokenAsync(bool forceRefresh, CancellationToken ct) { /* ... */ }
    public void Invalidate() { /* ... */ }
}

services.AddSingleton<RedisTokenProvider>();
services.AddDosaicRestClient<IUserApi>(o => o.BaseAddress = "...")
        .AddTokenProvider<RedisTokenProvider>();
```

## Resilience

### Standard (recommended)

```csharp
.AddStandardResilience();
```

Gives the `Microsoft.Extensions.Http.Resilience` standard pipeline: total request timeout, attempt timeout, retry with exp backoff + jitter, circuit breaker. Configurable via `IOptions`.

### Custom Polly v8 pipeline

```csharp
var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(200),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.TooManyRequests)
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();

services.AddDosaicRestClient<IUserApi>(o => o.BaseAddress = "...")
        .AddResilience(pipeline);
```

### Default pipeline (when nothing else is configured)

When you don't call `AddStandardResilience` or `AddResilience`, the **static factory path** falls back to `RestEaseDefaults.CreateDefaultPipeline()`:

- 3 retries, exponential backoff + jitter, base delay 250 ms
- Triggers on `HttpRequestException` and any 5xx / 408 / 429
- `Retry-After` header honoured (both delta-seconds and HTTP-date)
- 100 s overall timeout

> Note: in the DI path nothing is added implicitly — opt in with `.AddStandardResilience()`.

## Middleware (DelegatingHandler chain)

```csharp
public sealed class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"));
        return base.SendAsync(request, ct);
    }
}

services.AddDosaicRestClient<IUserApi>(o => o.BaseAddress = "...")
        .AddHandler<CorrelationIdHandler>()
        .AddOAuth2(a => { /* ... */ })
        .AddStandardResilience();
```

Handler ordering follows the `IHttpClientBuilder` chain — outer handlers wrap inner. The primary `SocketsHttpHandler` sits at the bottom and is managed by `IHttpClientFactory`.

## JSON

System.Text.Json is the only supported serializer. Defaults:

```csharp
new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter() }
};
```

Override globally per client:

```csharp
.ConfigureJson(j =>
{
    j.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    j.Converters.Add(new MyCustomConverter());
});
```

## Static Factory (non-DI)

```csharp
using Dosaic.Extensions.RestEase;

// Simplest
var api = RestClientFactory.Create<IUserApi>("https://api.example.com");

// With OAuth2
var api = RestClientFactory.Create<IUserApi>("https://api.example.com", authConfig);

// With a custom resilience pipeline
var api = RestClientFactory.Create<IUserApi>("https://api.example.com", pipeline);

// Full configuration
var api = RestClientFactory.Create<IUserApi>("https://api.example.com", o =>
{
    o.Timeout = TimeSpan.FromSeconds(20);
    o.UserAgent = "my-service/1.0";
    o.DefaultHeaders["X-Tenant"] = tenantId;
    o.Authentication = authConfig;
    o.ResiliencePipeline = pipeline;
    o.JsonOptions = customJson;
});
```

The static factory builds a fresh handler chain (`UserAgent → Resilience → OAuth2 → SocketsHttpHandler`) per call. Prefer the DI path in long-running apps to benefit from `IHttpClientFactory` pooling.

## Best Practices

- **Use the DI path** for hosted services. The static factory is for short-lived tools and tests.
- **Always call `.AddStandardResilience()`** (or a custom pipeline) on the DI path — there is no implicit retry/timeout otherwise.
- **One client interface per service**. Don't reuse the same interface for two upstreams — register each with a distinct name.
- **Set `RefreshSkew` ≥ 10 s** so tokens refresh before they expire on the wire. Default is 30 s.
- **Distributed token cache**: implement `ITokenProvider` backed by Redis/Vault when you run multiple replicas — otherwise each replica holds its own copy.
- **Don't combine `AddOAuth2` with manual `Authorization` headers** on every request. If you must override per-call, set `request.Headers.Authorization` — the handler honours it.
- **Custom handlers stay stateless** — `IHttpClientFactory` instantiates them per request scope; per-handler state will surprise you.
- **Use the same client name + key everywhere** — `AddDosaicRestClient<TApi>(name)` is keyed by name internally (options, token provider, http client all share it).
- **Override `JsonOptions` rather than rolling your own serializer** — System.Text.Json is the only path supported.

## API Reference

### `RestClientFactory` (static)

| Method | Description |
|---|---|
| `Create<T>(string baseAddress)` | Default pipeline, no auth |
| `Create<T>(string baseAddress, AuthenticationConfig)` | Adds OAuth2 |
| `Create<T>(string baseAddress, ResiliencePipeline<HttpResponseMessage>)` | Replaces resilience pipeline |
| `Create<T>(string baseAddress, AuthenticationConfig, ResiliencePipeline<HttpResponseMessage>)` | Auth + custom pipeline |
| `Create<T>(string baseAddress, Action<StandaloneClientOptions>)` | Full options bag |

### `RestEaseDefaults`

| Member | Description |
|---|---|
| `CreateDefaultJsonOptions()` | Web-mode STJ options w/ `JsonStringEnumConverter` |
| `CreateDefaultPipeline()` | Polly v8 retry + timeout pipeline (default for static factory) |

### `RestEaseClientOptions`

| Property | Type | Description |
|---|---|---|
| `BaseAddress` | `string` | API base URL |
| `Timeout` | `TimeSpan?` | `HttpClient.Timeout` |
| `UserAgent` | `string` | Appended to `User-Agent` header |
| `Authentication` | `AuthenticationConfig` | OAuth2 settings |
| `JsonOptions` | `JsonSerializerOptions` | Override STJ defaults |
| `DefaultHeaders` | `Dictionary<string,string>` | Static request headers |

### `AuthenticationConfig`

| Property | Type | Description |
|---|---|---|
| `Enabled` | `bool` | Master switch |
| `BaseUrl` | `string` | IdP base URL |
| `TokenUrlPath` | `string` | Token endpoint path |
| `GrantType` | `GrantType` | `ClientCredentials` · `Password` |
| `ClientId` / `ClientSecret` | `string` | OAuth2 client identity |
| `Username` / `Password` | `string` | Resource owner credentials |
| `Scope` / `Audience` | `string` | Optional scope / audience |
| `RefreshSkew` | `TimeSpan` | Refresh-buffer before real expiry. Default 30s |

### `ITokenProvider`

```csharp
public interface ITokenProvider
{
    Task<AccessToken> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
    void Invalidate();
}

public sealed class AccessToken
{
    public string TokenType { get; init; }
    public string Value { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
```
