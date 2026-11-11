# Dosaic.Plugins.Caching.Redis

`Dosaic.Plugins.Caching.Redis` is a Dosaic plugin that provides distributed caching backed by [Redis](https://redis.io/) via [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis). It registers `IDistributedCache` in the DI container, adds an OpenTelemetry tracing instrumentation for Redis operations, and wires up a readiness health check. For local development it can transparently fall back to an in-memory cache without any infrastructure dependency.

## Installation

```shell
dotnet add package Dosaic.Plugins.Caching.Redis
```

Or add a package reference manually:

```xml
<PackageReference Include="Dosaic.Plugins.Caching.Redis" Version="" />
```

## Features

- Registers `IDistributedCache` backed by **StackExchange.Redis** with a single config property
- **In-memory fallback** mode (`UseInMemory: true`) for local development and testing — no Redis instance required
- Automatic `abortConnect=false` safety flag appended to the connection string when not already present
- **OpenTelemetry** tracing instrumentation for every Redis command via `OpenTelemetry.Instrumentation.StackExchangeRedis`
- **Readiness health check** (`/health/readiness`) that verifies Redis connectivity (skipped in in-memory mode)
- **Key prefix** (`instanceName`) so several installations can share one Redis
- **Distributed rate limiting** — registers `IDistributedRateLimiterFactory`, used by `Dosaic.Extensions.RestEase` clients with `rateLimits:distributed: true`
- Configuration-driven — no boilerplate, just add the plugin and configure the section

## Configuration

The plugin binds to the `redisCache` section via the `[Configuration("redisCache")]` attribute on `RedisCacheConfiguration`.

### `RedisCacheConfiguration`

| Property | Type | Default | Description |
|---|---|---|---|
| `UseInMemory` | `bool` | `false` | When `true`, uses an in-memory cache instead of Redis. Useful for local development. |
| `ConnectionString` | `string` | *(required)* | StackExchange.Redis connection string (e.g. `localhost:6379`). Required unless `UseInMemory` is `true`. |
| `InstanceName` | `string` | `null` | Prefix for every key the plugin writes (`RedisCacheOptions.InstanceName` and the rate limiter keys). `null` = no prefix. Ignored in in-memory mode. |
| `RateLimitLeaseTtlInSeconds` | `int` | `120` | How long a permit of a distributed concurrency limiter stays taken once its holder stops renewing it (crashed process). Held permits are renewed three times per TTL, so calls may run longer than this. |

### appsettings.yml — Redis (production)

```yaml
redisCache:
  useInMemory: false
  connectionString: "redis-host:6379,password=s3cr3t,ssl=false"
  instanceName: "tenant-a:"          # optional key prefix, default: none
  rateLimitLeaseTtlInSeconds: 120    # optional, default: 120
```

### appsettings.yml — in-memory (local development)

```yaml
redisCache:
  useInMemory: true
```

### appsettings.json equivalent

```json
{
  "redisCache": {
    "useInMemory": false,
    "connectionString": "redis-host:6379,password=s3cr3t,ssl=false"
  }
}
```

> **Note:** When `UseInMemory` is `false`, the `ConnectionString` property is mandatory. The plugin will throw an `ArgumentException` at startup if it is missing or empty.

> **Note:** If `abortConnect=false` is not present in the connection string, the plugin appends it automatically to prevent the application from crashing when Redis is temporarily unavailable at startup.

## Usage

### Registering the plugin

The plugin is discovered automatically by the Dosaic source generator — no manual registration is needed. Just make sure the package is referenced and the `redisCache` section is present in your configuration.

### Injecting `IDistributedCache`

```csharp
using Microsoft.Extensions.Caching.Distributed;

internal class OrderService(IDistributedCache cache)
{
    private const string CacheKey = "orders:summary";

    public async Task<string?> GetSummaryAsync(CancellationToken ct)
    {
        return await cache.GetStringAsync(CacheKey, ct);
    }

    public async Task SetSummaryAsync(string summary, CancellationToken ct)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        await cache.SetStringAsync(CacheKey, summary, options, ct);
    }

    public async Task InvalidateSummaryAsync(CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKey, ct);
    }
}
```

### Storing and retrieving complex objects

`IDistributedCache` works with raw bytes. Serialize with `System.Text.Json` for structured data:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

internal class ProductCache(IDistributedCache cache)
{
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await cache.SetAsync(key, bytes, DefaultOptions, ct);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }
}
```

### Injecting `RedisCacheConfiguration`

The `RedisCacheConfiguration` singleton is also registered in DI if you need to read the connection string elsewhere:

```csharp
internal class RedisInfoService(RedisCacheConfiguration redisCacheConfiguration)
{
    public string GetConnectionInfo()
        => redisCacheConfiguration.UseInMemory
            ? "Running with in-memory cache"
            : $"Connected to Redis at {redisCacheConfiguration.ConnectionString}";
}
```

### Sharing one Redis between installations

Set `redisCache:instanceName` (for example `tenant-a:`) and every cache entry is stored as `tenant-a:<key>`. The value is exposed on the `RedisCacheConfiguration` singleton, so code that talks to `IConnectionMultiplexer` directly can apply the same prefix:

```csharp
internal class Counters(IConnectionMultiplexer redis, RedisCacheConfiguration configuration)
{
    public Task<long> IncrementAsync(string name) =>
        redis.GetDatabase().StringIncrementAsync($"{configuration.InstanceName}counter:{name}");
}
```

## Distributed rate limiting

The plugin registers `IDistributedRateLimiterFactory` (from `Dosaic.Extensions.Abstractions`) as a singleton, unless one is registered already:

| Mode | Implementation | Budget shared by |
|---|---|---|
| `useInMemory: false` | Redis | every process using the same Redis, `instanceName` and key |
| `useInMemory: true` | `InProcessRateLimiterFactory` | limiters with the same key inside this process |

`Dosaic.Extensions.RestEase` uses it for clients with `rateLimits:distributed: true`. You can also use it directly — it returns plain `System.Threading.RateLimiting.RateLimiter` instances:

```csharp
internal class Importer(IDistributedRateLimiterFactory limiters)
{
    private readonly RateLimiter _limiter = limiters.CreateSlidingWindow("partner-api",
        new SlidingWindowRateLimiterOptions { PermitLimit = 50, Window = TimeSpan.FromSeconds(10), SegmentsPerWindow = 4 });

    public async Task CallAsync(CancellationToken ct)
    {
        using var lease = await _limiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired) return; // lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
        // ...
    }
}
```

How the Redis limiters behave:

- **Atomic and clock independent.** Every decision is one Lua script. Time is read from the Redis server (`TIME`), so clock skew between pods does not matter.
- **Keys.** State is stored under `{instanceName}ratelimit:{key}:{algorithm}` with `algorithm` being `sliding`, `fixed`, `token` or `concurrency` — the suffix lets one key be used for several algorithms. Every key carries a TTL and disappears when the limiter is idle.
- **Algorithms.** Sliding window (permits return when their segment leaves the window), fixed window (starts with the first request), token bucket (`TokensPerPeriod` every `ReplenishmentPeriod`) and concurrency follow their BCL counterparts. `AutoReplenishment` is ignored: replenishment is always driven by the server clock.
- **Concurrency leases are renewed while held, and expire otherwise.** A permit is returned when its lease is disposed. While it is held, the limiter extends it three times per `rateLimitLeaseTtlInSeconds`, so a call may run longer than the TTL. Once the holder is gone — the pod crashed, the limiter was disposed, or the lease was dropped without `Dispose` — nothing renews it and the permit comes back after the TTL. A renewal never brings back a permit that already expired and was handed to somebody else.
- **Waiting.** With `QueueLimit > 0`, `AcquireAsync` waits by polling with a jittered delay until it gets its permits or the token is cancelled — time based limiters wait for the `RetryAfter` the script reports, the concurrency limiter polls every 25–500 ms. `QueueLimit` bounds the waiting permits of one limiter instance, not of the whole installation, and `QueueProcessingOrder` is not honoured. When the queue is full, or `QueueLimit` is 0, a failed lease with `RetryAfter` metadata is returned. `AttemptAcquire` never waits.
- **Redis errors.** If Redis cannot be reached or times out, a warning is logged and an in-process limiter with the same options takes over. Redis is tried again after 5 seconds. During that time every process enforces the limit on its own.
- **Statistics.** `GetStatistics()` returns `null`, a local snapshot would not describe the shared budget.

Requires Redis 5 or newer (scripts that read `TIME` before writing).

## Health Checks

When `UseInMemory` is `false`, the plugin registers a Redis health check tagged with the `readiness` tag. This means the `/health/readiness` endpoint (provided by the Dosaic WebHost) will report `Unhealthy` if Redis is unreachable.

```
GET /health/readiness

{
  "status": "Healthy",
  "results": {
    "redis": {
      "status": "Healthy",
      "description": null
    }
  }
}
```

No health check is registered in in-memory mode.

## OpenTelemetry

When running in Redis mode, the plugin automatically adds `StackExchangeRedis` tracing instrumentation to the OpenTelemetry pipeline. Every Redis command (GET, SET, DEL, etc.) will appear as a span in your distributed trace, including the command name and target key.

No additional configuration is required — instrumentation is enabled as part of `ConfigureServices`.

## Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.Extensions.Caching.StackExchangeRedis` | `IDistributedCache` backed by Redis |
| `AspNetCore.HealthChecks.Redis` | Redis readiness health check |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | Distributed tracing for Redis commands |
| `OpenTelemetry.Extensions.Hosting` | OpenTelemetry hosting integration |
| `Dosaic.Hosting.Abstractions` | Dosaic plugin interfaces and attributes |
| `Dosaic.Extensions.Abstractions` | `IDistributedRateLimiterFactory` and the in-process fallback |

