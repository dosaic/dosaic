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
- Configuration-driven — no boilerplate, just add the plugin and configure the section

## Configuration

The plugin binds to the `redisCache` section via the `[Configuration("redisCache")]` attribute on `RedisCacheConfiguration`.

### `RedisCacheConfiguration`

| Property | Type | Default | Description |
|---|---|---|---|
| `UseInMemory` | `bool` | `false` | When `true`, uses an in-memory cache instead of Redis. Useful for local development. |
| `ConnectionString` | `string` | *(required)* | StackExchange.Redis connection string (e.g. `localhost:6379`). Required unless `UseInMemory` is `true`. |

### appsettings.yml — Redis (production)

```yaml
redisCache:
  useInMemory: false
  connectionString: "redis-host:6379,password=s3cr3t,ssl=false"
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

