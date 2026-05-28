# Dosaic.Extensions.RestEase

`Dosaic.Extensions.RestEase` builds typed HTTP API clients on top of [RestEase](https://github.com/canton7/RestEase). It plugs into `IHttpClientFactory`, uses **System.Text.Json**, supports a composable **DelegatingHandler middleware chain**, **Polly v8 resilience pipelines** (via `Microsoft.Extensions.Http.Resilience`), **response caching** via `IDistributedCache` (in-memory or Redis), and ships an **OAuth2** integration with thread-safe token caching, automatic refresh-token rotation, and 401-triggered force-refresh retry.

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
- **Response caching** — Polly v8 strategy backed by `IDistributedCache`. In-memory by default, swap to Redis / SQL Server / NCache with one line. Honours `Cache-Control` (`max-age`, `no-store`, `no-cache`, `private`).
- **Client-side rate limiting** — Polly v8 strategies (SlidingWindow / FixedWindow / TokenBucket / Concurrency) backed by `System.Threading.RateLimiting`. Throws `RateLimiterRejectedException` on rejection.
- **Single Polly pipeline** — caching + retry + rate limit + timeout all composed into one `ResiliencePipeline<HttpResponseMessage>` mounted via `AddResilienceHandler`. Auto-wired from `Caching` / `Resilience` / `RateLimits` config blocks. Custom strategies via `AddPolly(Action<...>)`.
- **OAuth2** out of the box — `ClientCredentials` and `Password` grants; transparent refresh-token rotation; concurrent-call coalescing via `SemaphoreSlim`; 401-triggered forced refresh + retry.
- **Pluggable `ITokenProvider`** — swap in a distributed token cache, mTLS, API key, or any custom auth strategy.
- **System.Text.Json only** — override `JsonSerializerOptions` per-client; sane web defaults out of the box.
- **DI builder fluent API** — `AddRestEaseApi<TApi>()` returns `IRestEaseClientBuilder` for composition.
- **Static factory for non-DI usage** — `RestClientFactory.Create<TApi>(...)` still available.

## Quick Start (DI — recommended)

```csharp
using Dosaic.Extensions.RestEase.DependencyInjection;

services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddResilience();
```

Resolve the typed client:

```csharp
public class UserService(IUserApi api)
{
    public Task<User> Get(Guid id, CancellationToken ct) => api.GetUserAsync(id, ct);
}
```

### Without the Options pattern

Pass a `RestEaseClientOptions` instance directly — no lambda, no `IConfiguration` binding:

```csharp
var options = new RestEaseClientOptions
{
    BaseAddress = "https://api.example.com",
    Timeout = TimeSpan.FromSeconds(30),
    UserAgent = "my-service/1.0"
};
options.DefaultHeaders["X-Tenant"] = tenantId;

services.AddRestEaseApi<IUserApi>(options);
```

Overload signatures:

```csharp
AddRestEaseApi<TApi>(this IServiceCollection, RestEaseClientOptions);
AddRestEaseApi<TApi>(this IServiceCollection, string name, RestEaseClientOptions);
```

The instance is copied into the named-options store at registration time. `IOptionsMonitor<RestEaseClientOptions>` is still wired internally (handlers depend on it) — caller does not touch it.

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
  Caching:
    Enabled: true
    DefaultTtl: 00:05:00
    MaxTtl: 00:30:00
    RespectCacheControl: true
    IncludeAuthorizationInKey: false
    KeyPrefix: "myapi:"
    Methods: [GET, HEAD]
    CacheableStatusCodes: [200, 404]
  Resilience:
    Enabled: true
    MaxRetryAttempts: 3
    BaseDelay: 00:00:00.200
    AttemptTimeout: 00:00:10
    TotalRequestTimeout: 00:00:30
  RateLimits:
    Enabled: true
    ThrowOnRejection: false       # false = return 429 + Retry-After, true = throw HttpRequestException
    SlidingWindow:
      Enabled: true
      PermitLimit: 50             # max requests per window
      Window: 00:00:10            # window size
      SegmentsPerWindow: 4        # window slices for smoothing
      AutoReplenishment: true
      QueueProcessingOrder: OldestFirst   # OldestFirst | NewestFirst
      QueueLimit: 1024
    FixedWindow:
      Enabled: false
      PermitLimit: 100
      Window: 00:00:01
      AutoReplenishment: true
      QueueLimit: 0
    TokenBucket:
      Enabled: false
      PermitLimit: 100            # token bucket capacity
      TokensPerPeriod: 10
      ReplenishmentPeriod: 00:00:01
      AutoReplenishment: true
      QueueLimit: 0
    Concurrency:
      Enabled: true
      PermitLimit: 10             # max in-flight requests
      QueueLimit: 1024
```

Multiple limiters enabled simultaneously stack as Polly strategies in single pipeline. Order: `Cache → SlidingWindow → FixedWindow → TokenBucket → Concurrency → TotalTimeout → Retry → AttemptTimeout` (outermost-first).

```csharp
services.AddRestEaseApiFromConfiguration<IUserApi>(configuration, "MyApi");
// single Polly pipeline auto-wired with cache + limiters + retry + timeouts
```

Extra Polly strategies via `.AddPolly((pb, ctx) => pb.AddHedging(...))` — stacks alongside auto-wired pipeline.

Overload signatures:

```csharp
AddRestEaseApiFromConfiguration<TApi>(IServiceCollection, IConfiguration, string sectionKey);
AddRestEaseApiFromConfiguration<TApi>(IServiceCollection, string name, IConfiguration, string sectionKey);
AddRestEaseApiFromConfiguration<TApi>(IServiceCollection, IConfigurationSection);
AddRestEaseApiFromConfiguration<TApi>(IServiceCollection, string name, IConfigurationSection);
```

## DI Builder API

`AddRestEaseApi<TApi>()` returns an `IRestEaseClientBuilder`:

| Method | Purpose |
|---|---|
| `.ConfigureOptions(Action<RestEaseClientOptions>)` | Mutate client options |
| `.ConfigureJson(Action<JsonSerializerOptions>)` | Tweak the System.Text.Json options |
| `.ConfigureHttpClient(Action<HttpClient>)` | Raw `HttpClient` configuration |
| `.AddOAuth2(Action<AuthenticationConfig>)` | Enable the built-in OAuth2 token provider |
| `.AddTokenProvider<T>()` | Plug in a custom `ITokenProvider` (registered in DI) |
| `.AddHandler<THandler>()` | Insert a `DelegatingHandler` into the chain (outside Polly pipeline) |
| `.AddResilience(Action<ResilienceConfig>?)` | Enable + tune retry / timeouts |
| `.AddCaching(Action<HttpCacheOptions>?)` | Enable + tune response cache (`IDistributedCache`-backed Polly strategy) |
| `.AddRateLimits(Action<RateLimitsConfig>?)` | Enable + tune rate limiters (SlidingWindow / FixedWindow / TokenBucket / Concurrency) |
| `.AddPolly(Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext>)` | Mount additional Polly v8 pipeline (separate slot, stacks with auto-pipeline) |

## OAuth2

### Client Credentials

```csharp
services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddOAuth2(a =>
        {
            a.BaseUrl = "https://auth.example.com";
            a.TokenUrlPath = "/oauth/token";
            a.GrantType = GrantType.ClientCredentials;
            a.ClientId = "my-client";
            a.ClientSecret = "s3cr3t";
            a.Scope = "api.read";
        })
        .AddResilience();
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
services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "...")
        .AddTokenProvider<RedisTokenProvider>();
```

## Resilience

Built on Polly v8 + `Microsoft.Extensions.Http.Resilience`. Three equivalent ways to enable:

**Builder method (most concise):**

```csharp
services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddResilience(r =>
        {
            r.MaxRetryAttempts = 3;
            r.BaseDelay = TimeSpan.FromMilliseconds(200);
            r.AttemptTimeout = TimeSpan.FromSeconds(10);
            r.TotalRequestTimeout = TimeSpan.FromSeconds(30);
        });
```

**Inline options:**

```csharp
services.AddRestEaseApi<IUserApi>(o =>
{
    o.BaseAddress = "https://api.example.com";
    o.Resilience = new ResilienceConfig { Enabled = true, MaxRetryAttempts = 3 };
});
```

**Config-driven:** see [Configuration Binding](#configuration-binding) section.

Defaults: exponential backoff + jitter, `HttpRetryStrategyOptions` predicate (5xx + 408 + 429 + `HttpRequestException`, honours `Retry-After`).

### `ResilienceConfig`

| Property | Type | Description |
|---|---|---|
| `Enabled` | `bool` | Master switch (`AddResilience()` sets to `true`) |
| `MaxRetryAttempts` | `int?` | Defaults to 3 |
| `BaseDelay` | `TimeSpan?` | Defaults to 500ms |
| `AttemptTimeout` | `TimeSpan?` | Per-try timeout |
| `TotalRequestTimeout` | `TimeSpan?` | Outermost timeout across retries |
| `AdditionalRetryStatusCodes` | `HashSet<HttpStatusCode>` | Extra retryable codes appended to default predicate |
| `ConfigureRetry` | `Action<HttpRetryStrategyOptions>?` | Last-mile mutation of retry strategy (backoff, jitter, custom `ShouldHandle`, `OnRetry` hooks) |

### Advanced retry tuning

```csharp
.AddResilience(r =>
{
    r.MaxRetryAttempts = 5;
    r.AdditionalRetryStatusCodes.Add(HttpStatusCode.BadGateway);
    r.ConfigureRetry = retry =>
    {
        retry.BackoffType = DelayBackoffType.Exponential;
        retry.UseJitter = true;
        retry.MaxDelay = TimeSpan.FromSeconds(30);
        retry.OnRetry = args => { /* log */ return default; };
    };
});
```

### Custom Polly pipeline (hedging / circuit breaker / etc.)

```csharp
services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "...")
        .AddPolly((pb, ctx) =>
        {
            pb.AddHedging(new HedgingStrategyOptions<HttpResponseMessage> { MaxHedgedAttempts = 2 });
            pb.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage> { FailureRatio = 0.1 });
        });
```

`AddPolly` mounts an additional resilience handler (separate slot — multiple calls produce stacked pipelines). Auto-pipeline (from `AddResilience` / `AddCaching` / `AddRateLimits`) always runs alongside.

### Static factory (non-DI)

`RestClientFactory.Create<T>` uses Polly directly via `ResiliencePipeline<HttpResponseMessage>` — defaults from `RestEaseDefaults.CreateDefaultPipeline()` (3 retries exp + jitter, 100s overall timeout).

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

services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "...")
        .AddHandler<CorrelationIdHandler>()
        .AddOAuth2(a => { /* ... */ })
        .AddResilience();
```

Custom `DelegatingHandler` chain wraps the Polly resilience handler at the outside. Order:

```
[CorrelationId] → [OAuth2] → [Polly pipeline: cache → limiters → retry → timeout] → SocketsHttpHandler
```

Custom handlers run on every retry (since they're outside the Polly pipeline). Put cache inside the Polly pipeline (via `Caching` options) if you want hits to short-circuit auth + retries.

## Response Caching

Cache implemented as Polly v8 strategy (`HttpCacheResilienceStrategy`) backed by `IDistributedCache`. Same abstraction covers in-process and out-of-process stores — swap backing implementation, strategy code unchanged.

### In-memory (default)

```csharp
services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddCaching(c => c.DefaultTtl = TimeSpan.FromMinutes(10));
```

If no `IDistributedCache` is already registered, `AddDistributedMemoryCache()` is auto-registered (per-process, no shared state).

### Redis (shared across replicas)

```csharp
services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = "redis:6379";
    o.InstanceName = "my-service:";
});

services.AddRestEaseApi<IUserApi>(o => o.BaseAddress = "https://api.example.com")
        .AddCaching(c => c.DefaultTtl = TimeSpan.FromMinutes(10));
```

Any `IDistributedCache` implementation works: `Microsoft.Extensions.Caching.StackExchangeRedis`, `Microsoft.Extensions.Caching.SqlServer`, `NCache`, etc. Register **before** `AddRestEaseApi<T>` so the cache strategy resolves your impl instead of the in-memory fallback.

### `HttpCacheOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Master switch |
| `DefaultTtl` | `TimeSpan` | `5 min` | TTL when no `Cache-Control: max-age` on response |
| `MaxTtl` | `TimeSpan?` | `null` | Upper bound — clamps server-provided `max-age` |
| `Methods` | `HashSet<string>` | `{ "GET" }` | HTTP method names to cache (case-insensitive) |
| `CacheableStatusCodes` | `HashSet<int>` | `200, 203, 300, 301, 404, 410` | Statuses eligible for storage |
| `RespectCacheControl` | `bool` | `true` | Honour `no-store`, `no-cache`, `private`, `max-age` |
| `IncludeAuthorizationInKey` | `bool` | `false` | When `true`, SHA256-hashed `Authorization` header gets folded into key |
| `KeyPrefix` | `string` | `dosaic:restease:` | Prefix prepended to every key |
| `KeyBuilder` | `Func<HttpRequestMessage,string>?` | `null` | Override default `method + url + auth-hash` key. **Code-only** — not bindable from config |
| `ShouldCacheRequest` | `Func<HttpRequestMessage,bool>?` | `null` | Per-request opt-out. **Code-only** |
| `ShouldCacheResponse` | `Func<HttpResponseMessage,bool>?` | `null` | Per-response opt-out. **Code-only** |

### Config-driven caching

`HttpCacheOptions` lives on `RestEaseClientOptions.Caching` — bind from same config section:

```yaml
Api:
  BaseAddress: https://api.example.com
  Caching:
    Enabled: true
    DefaultTtl: 00:10:00
    KeyPrefix: "myapi:"
    Methods: [GET, HEAD]
```

```csharp
services.AddRestEaseApiFromConfiguration<IUserApi>(configuration, "Api");
// Caching block auto-wires into Polly pipeline
```

For code-only fields (`KeyBuilder`, `ShouldCacheRequest`, `ShouldCacheResponse`), layer `.AddCaching(...)` after config-binding:

```csharp
services.AddRestEaseApiFromConfiguration<IUserApi>(configuration, "Api")
        .AddCaching(c => c.KeyBuilder = req => $"v2:{req.RequestUri}");
```

### Cache-Control semantics

- Request `Cache-Control: no-store` or `no-cache` → bypass cache
- Response `Cache-Control: no-store`, `no-cache`, or `private` → not stored
- Response `Cache-Control: max-age=N` → TTL = `min(N, MaxTtl ?? N)`
- No `Cache-Control` on response → `DefaultTtl`

### Strategy ordering (inside Polly pipeline)

Pipeline order: `Cache → RateLimiters → TotalTimeout → Retry → AttemptTimeout`. Cache outermost — hit short-circuits all other strategies + the inner HTTP call. OAuth2 lives in the `DelegatingHandler` chain *outside* the Polly pipeline — cache hit skips token fetch automatically.

### Key construction

Default key: `KeyPrefix + "{METHOD} {ABSOLUTE-URL}"`.

`Authorization` header is **excluded** by default — same response served to every caller. Opt-in via `IncludeAuthorizationInKey = true` to isolate per-token (header value is SHA256-hashed, never stored raw).

Custom key:

```csharp
.AddCaching(c => c.KeyBuilder = req =>
    $"{req.Method.Method}:{req.RequestUri}:tenant={req.Headers.GetValues("X-Tenant").First()}");
```

### Storage format

Each cache entry is a JSON envelope: status code, response headers, content headers, body bytes. Easy to inspect in Redis with `GET key | jq`.

### Why `IDistributedCache`?

One abstraction covers in-process **and** out-of-process. `IMemoryCache` would lock you into a single replica. If you only need L1, `AddDistributedMemoryCache` is a `MemoryDistributedCache` — same in-memory performance, swappable later without code change. Two-tier (L1+L2) is out of scope here — use `HybridCache` if you need it and wire your own handler.

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
- **Call `.AddResilience()`** (or `.AddCaching()` / `.AddRateLimits()`, or enable equivalent option blocks) — auto-pipeline mounts unconditionally but adds zero strategies when no block is enabled.
- **One client interface per service**. Don't reuse the same interface for two upstreams — register each with a distinct name.
- **Set `RefreshSkew` ≥ 10 s** so tokens refresh before they expire on the wire. Default is 30 s.
- **Distributed token cache**: implement `ITokenProvider` backed by Redis/Vault when you run multiple replicas — otherwise each replica holds its own copy.
- **Don't combine `AddOAuth2` with manual `Authorization` headers** on every request. If you must override per-call, set `request.Headers.Authorization` — the handler honours it.
- **Custom handlers stay stateless** — `IHttpClientFactory` instantiates them per request scope; per-handler state will surprise you.
- **Use the same client name + key everywhere** — `AddRestEaseApi<TApi>(name)` is keyed by name internally (options, token provider, http client all share it).
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
