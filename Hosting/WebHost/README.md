# Dosaic.Hosting.WebHost

![Nuget](https://img.shields.io/nuget/v/Dosaic.Hosting.WebHost?style=flat-square)
![Nuget](https://img.shields.io/nuget/dt/Dosaic.Hosting.WebHost?style=flat-square)

`Dosaic.Hosting.WebHost` is the **core orchestration package** for any Dosaic-based service. It wires together plugin discovery, dependency injection, middleware pipeline configuration, health checks, OpenTelemetry, structured logging, CORS, response compression, IP rate limiting, and endpoint routing — all driven by the plugin interfaces from `Dosaic.Hosting.Abstractions`.

> **This package is mandatory.** Without it no plugins can be loaded or configured.

---

## Installation

```shell
dotnet add package Dosaic.Hosting.Generator  # required for AOT-compatible plugin discovery
dotnet add package Dosaic.Hosting.WebHost
```

Or as package references in your `.csproj`:

```xml
<PackageReference Include="Dosaic.Hosting.Generator" Version="" />
<PackageReference Include="Dosaic.Hosting.WebHost" Version="" />
```

---

## Usage

Replace the content of `Program.cs` with:

```csharp
using Dosaic.Hosting.WebHost;

PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

`DosaicPluginTypes.All` is a `Type[]` emitted at **compile time** by `Dosaic.Hosting.Generator` — no runtime reflection scanning is required.

For advanced scenarios (e.g. integration testing) you can separate build from run:

```csharp
using Dosaic.Hosting.WebHost;

var host = PluginWebHostBuilder.Create(Dosaic.Generated.DosaicPluginTypes.All, args).Build();
host.Run();
```

---

## Features

| Feature | Details |
|---|---|
| **AOT-compatible plugin discovery** | Plugin types resolved from source-generated `Type[]` — no reflection at runtime |
| **Structured logging** | Serilog with span/thread enrichers, structured request logging |
| **OpenTelemetry** | Metrics, tracing (OTLP), and logging exporters; Prometheus scraping endpoint at `/metrics` |
| **Health checks** | Built-in liveness checks (API, disk, memory); plugin and attribute-driven registrations |
| **CORS** | Configurable policy via `CorsPolicy` config section |
| **Response compression** | Brotli + Gzip (fastest level), applied to HTTP and HTTPS |
| **IP rate limiting** | AspNetCoreRateLimit integration, configurable via `ipRateLimiting` section |
| **Layered configuration** | JSON, YAML, secrets files, env vars, CLI args — ordered by specificity |
| **YAML input/output formatters** | Controllers accept and return `application/yaml` in addition to JSON |
| **Forwarded headers** | All forwarded headers trusted (proxy-friendly) |
| **Configurable Kestrel** | Listening URLs and max request body size controlled via config |

---

## Plugin Lifecycle

`PluginWebHostBuilder.Build()` executes the following phases in order:

### 1 — Host Configuration (`HostConfigurator`)

- Clears default configuration sources and rebuilds them in the defined loading order (see [Configuration](#configuration) below)
- Configures **Kestrel**: listening URLs (`host:urls`), max request body size (`host:maxRequestSize`), server header suppression
- Bootstraps **Serilog** structured logging

### 2 — Service Configuration (`ServiceConfigurator`)

- Registers default services: JSON serialization, memory cache, datetime providers, forwarded headers
- Configures **CORS**, **response compression**, **IP rate limiting**
- Adds **MVC controllers** with YAML formatters and custom validation error responses
- Discovers and registers `[Configuration(...)]`-attributed classes from `IConfiguration`
- Registers **health checks**: built-in liveness checks + `[HealthCheck]`-attributed types + plugin-contributed checks
- Configures **OpenTelemetry** metrics, tracing, and logging
- Calls `IPluginServiceConfiguration.ConfigureServices()` on all discovered plugins (sorted by origin)

### 3 — App Build

`WebApplicationBuilder.Build()` finalises the DI container.

### 4 — Application Configuration (`AppConfigurator`)

- Enables forwarded headers, response compression, IP rate limiting, CORS, Prometheus endpoint, routing
- Registers `[Middleware]`-attributed types ordered by their `Order` property
- Calls `IPluginApplicationConfiguration.ConfigureApplication()` on all discovered plugins
- Maps health endpoints (`/health/liveness`, `/health/readiness`)
- Maps controllers
- Calls `IPluginEndpointsConfiguration.ConfigureEndpoints()` on all discovered plugins

### 5 — Instance Cleanup

`TypeImplementationResolver.ClearInstances()` disposes all plugin constructor instances created during build.

### Plugin Sort Order

| Plugin origin | Sort key | Runs |
|---|---|---|
| `Dosaic.*` namespace | `sbyte.MinValue` | First |
| Third-party plugins | `0` | Middle |
| Host assembly plugins | `sbyte.MaxValue` | Last |

---

## Configuration

### Kestrel / Host

```yaml
host:
  urls: "http://+:8080"          # default; separate multiple URLs with ","
  maxRequestSize: 8388608        # default 8 MB
```

Environment variables (single `_` maps to `:` hierarchy):

```shell
HOST_URLS=http://+:8080
HOST_MAXREQUESTSIZE=8388608
```

### Configuration File Loading Order

Dosaic loads configuration in the following order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.yaml` / `appsettings.yml`
3. `appsettings.*.json`
4. `appsettings.*.yaml` / `appsettings.*.yml`
5. `appsettings.secrets.json` / `appsettings.secrets.yaml` / `appsettings.secrets.yml`
6. `appsettings.*.secrets.json` / `appsettings.*.secrets.yaml` / `appsettings.*.secrets.yml`
7. Environment variables
8. Command-line arguments

Files are sorted by node count (number of `.`-separated segments) within each group. Secrets files always load after non-secrets files. Files whose names do not start with `appsettings` are ignored.

#### Additional Config Paths

You can load configuration files from additional directories. Those files are loaded **before** the main application directory.

Via environment variable:

```shell
HOST_ADDITIONALCONFIGPATHS=/path/to/configs,/another/path
```

Via command-line argument:

```shell
dotnet run --HOST:ADDITIONALCONFIGPATHS "/path/to/configs,/another/path"
```

- Subfolders are scanned recursively for `appsettings.*` files
- Supports `.json`, `.yaml`, `.yml` formats
- Non-existent paths raise a `DirectoryNotFoundException`

#### Environment Variable Mapping

Dosaic uses a custom provider that maps environment variable names to configuration keys by replacing `_` with `:` (hierarchy separator). Double underscore `__` is first collapsed to single `_` before the mapping.

```shell
HOST_URLS              ->  host:urls
SERILOG_minimumLevel   ->  serilog:minimumLevel
```

### Logging (Serilog)

```yaml
serilog:
  minimumLevel: Information   # Verbose | Debug | Information | Warning | Error | Fatal
  override:
    System: Error
    Microsoft: Error
```

Environment variables:

```shell
SERILOG_minimumLevel=Information
SERILOG_OVERRIDE_SYSTEM=Error
SERILOG_OVERRIDE_MICROSOFT=Error
```

### CORS

```yaml
CorsPolicy:
  origins:
    - "https://example.com"
  methods:
    - "GET"
    - "POST"
  headers:
    - "*"
  exposedHeaders:
    - "*"
```

If any of `origins`, `methods`, `headers`, or `exposedHeaders` are not specified, they default to `["*"]`.

### IP Rate Limiting

```yaml
ipRateLimiting:
  enableEndpointRateLimiting: true
  generalRules:
    - endpoint: "*"
      period: "1s"
      limit: 50

ipRateLimitPolicies: {}
```

Refer to [AspNetCoreRateLimit documentation](https://github.com/stefanprodan/AspNetCoreRateLimit) for the full schema.

### OpenTelemetry

When `telemetry:endpoint` is configured, traces, metrics, and logs are exported via OTLP. Log messages are enriched with `SpanId` and `TraceId`.
Use `telemetry:name` to explicitly set the service name used by OpenTelemetry resources for traces, metrics, and logs.

```yaml
telemetry:
  name: Dosaic.Example.Service    # optional OpenTelemetry service name
  endpoint: http://localhost:4317 # OTLP endpoint
  protocol: grpc                  # grpc | http/protobuf
  headers:
    - name: Authorization
      value: "Bearer <token>"
```

Without `telemetry:endpoint`, only the Prometheus scraping endpoint (`/metrics`) is active.

Metrics instrumentation (always active):
- ASP.NET Core request metrics
- HTTP client metrics
- .NET runtime metrics
- Process metrics
- All custom `ActivitySource` meters (`*`)

Tracing instrumentation (requires `telemetry:endpoint`):
- ASP.NET Core (Swagger paths excluded)
- HTTP client
- All custom `ActivitySource` sources (`*`)
- B3 propagation (single and multi-header)

---

## Health Endpoints

| Endpoint | Tags filtered |
|---|---|
| `GET /health/liveness` | `liveness` |
| `GET /health/readiness` | `readiness` |

Both endpoints return a JSON body:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.012",
  "entries": [
    {
      "name": "api",
      "status": "Healthy",
      "description": null,
      "tags": ["liveness"],
      "duration": "00:00:00.001",
      "exceptionMessage": null
    }
  ]
}
```

Built-in liveness checks registered automatically:

| Check | Description |
|---|---|
| `api` | Always healthy — confirms the process is running |
| `disk_space` | Checks free space on all drives |
| `memory` | Asserts allocated memory is below 2 GB |

Plugins can contribute additional checks via `IPluginHealthChecksConfiguration.ConfigureHealthChecks()`.
Custom checks can also be auto-discovered by implementing `IHealthCheck` and annotating the class with `[HealthCheck("name", ...tags)]`.
