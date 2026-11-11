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
  additionalConfigPaths: ""      # see "Additional Config Paths"
  healthChecks:
    memory:
      maximumMegabytes: 2048     # default; liveness threshold, 0 = no memory check (see "Health Endpoints")
```

Environment variables (single `_` maps to `:` hierarchy):

```shell
HOST_URLS=http://+:8080
HOST_MAXREQUESTSIZE=8388608
HOST_HEALTHCHECKS_MEMORY_MAXIMUMMEGABYTES=2048
```

The whole section is typed as `HostConfiguration` (`Dosaic.Hosting.Abstractions.Configuration`). The host
reads it once, registers it as a singleton and hands the same instance to plugin constructors:

```csharp
public class MyPlugin(HostConfiguration hostConfiguration) : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        var limit = hostConfiguration.MaxRequestSize;                             // 8388608 unless configured
        var memory = hostConfiguration.HealthChecks.Memory.MaximumMegabytes;      // 2048 unless configured
    }
}
```

| Key | Property | Default |
|---|---|---|
| `host:urls` | `Urls` | `null` → `aspnetcore:urls` → `http://+:8080` |
| `host:maxRequestSize` | `MaxRequestSize` | `8388608` |
| `host:additionalConfigPaths` | `AdditionalConfigPaths` | `null` |
| `host:healthChecks:memory:maximumMegabytes` | `HealthChecks.Memory.MaximumMegabytes` | `2048`, `0` = no check |

A missing `host` section, missing keys and explicit `null` values all keep the defaults.

### Configuration schema (editor completion)

Start the service with `--write-config-schema [path]` and it writes a JSON schema of its configuration
instead of starting. The default path is `appsettings.schema.json` in the working directory:

```shell
dotnet run -- --write-config-schema
dotnet run -- --write-config-schema ./config/appsettings.schema.json
```

The schema is built from the `[Configuration]` classes of the plugins the service is built with, plus the
sections the web host reads itself (`host`, `serilog`, `telemetry`, `CorsPolicy`). It carries types, enum
values, defaults and descriptions, so it always matches the installed packages.

- It runs before anything is configured: no database, no connection strings and no valid configuration are
  needed.
- The process arguments are read directly, so `args` do not have to be passed to `PluginWebHostBuilder`.
  A `Program.cs` that hands them to something else afterwards (`.Build().RunWithGraphQLCommands(args)`)
  works unchanged — the process ends before that code runs.
- From code: `PluginWebHostBuilder.WriteConfigurationSchema(DosaicPluginTypes.All, path)`.

Reference the schema from the files that should get completion:

```yaml
# yaml-language-server: $schema=./appsettings.schema.json
host:
  maxRequestSize: 8388608
```

```json
{
  "$schema": "./appsettings.schema.json"
}
```

YAML completion needs an editor with the YAML language server (VS Code with the Red Hat YAML extension,
JetBrains Rider). Commit the schema file and regenerate it after package updates.

Descriptions come from the XML documentation of the packages. NuGet does not copy those files to the
output by default; add this to the service project to get them:

```xml
<PropertyGroup>
  <CopyDocumentationFilesFromPackages>true</CopyDocumentationFilesFromPackages>
</PropertyGroup>
```

What the schema cannot do:

- Keys are offered in camel case. Configuration keys are not case sensitive, a JSON schema is — a key in
  another casing is not flagged, it just gets no completion.
- Nothing is marked as required or forbidden, so the schema never rejects a file the service would accept.
- Sections with a name of your choice (for example RestEase clients) and third party sections
  (`ipRateLimiting`) are not described.
- Environment variables get no completion.

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
- All custom `Meter`s (`*`) plus the shared `Dosaic` meter (`Tracing.SourceName`)

Tracing instrumentation (requires `telemetry:endpoint`):
- ASP.NET Core (Swagger paths excluded)
- HTTP client
- The shared `Dosaic` `ActivitySource` (`Tracing.SourceName`) — emit your own spans with `Tracing.StartActivity(...)`; see [Hosting/Abstractions/README.md](../../Hosting/Abstractions/README.md#tracing)
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
| `memory` | Asserts allocated memory (`GC.GetTotalMemory`) is below the configured threshold, 2 GB by default |

A failing liveness check makes Kubernetes restart the pod, so the memory threshold should match the
memory limit of the deployment:

```yaml
host:
  healthChecks:
    memory:
      maximumMegabytes: 2048     # default; 0 = do not register the memory check
```

```shell
HOST_HEALTHCHECKS_MEMORY_MAXIMUMMEGABYTES=6144
```

`api` and `disk_space` are always registered. A negative value stops the host at startup.

Plugins can contribute additional checks via `IPluginHealthChecksConfiguration.ConfigureHealthChecks()`.
Custom checks can also be auto-discovered by implementing `IHealthCheck` and annotating the class with `[HealthCheck("name", ...tags)]`.
