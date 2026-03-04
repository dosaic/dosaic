# AGENTS.md — Dosaic AI Agent Guide

> Canonical reference for AI coding agents working in the Dosaic repository.

## Project Overview

**Dosaic** is a plugin-first .NET framework for rapidly building anything hosted in the web.
The name is a play on "mosaic" — Dotnet Orchestration Services Abstraction Integration Configuration.

- **Target framework:** net10.0 (SDK 10.0.103 via `global.json`)
- **License:** MIT
- **NuGet packages:** published as `Dosaic.*`

### Entry Point

```csharp
using Dosaic.Hosting.WebHost;
PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

`DosaicPluginTypes.All` is emitted at compile time by the source generator — no runtime reflection scanning.

### Solution Layout

```
Hosting/
  Abstractions/    → Core plugin interfaces, attributes, services, middlewares
  Generator/       → IIncrementalGenerator emitting DosaicPluginTypes.All
  WebHost/         → PluginWebHostBuilder, Configurators, TypeImplementationResolver

Plugins/
  Authorization/   → Keycloak integration
  Caching/         → Redis distributed cache
  Endpoints/       → REST resource entity abstractions
  Handlers/        → CQRS abstractions + implementation
  Jobs/            → Hangfire scheduler
  Management/      → Unleash feature flags
  Mapping/         → Mapster auto-mapping
  Messaging/       → MassTransit messaging
  Persistence/     → EF Core, MongoDB, InMemory, S3, VaultSharp
  Storage/         → SMB file storage
  Validations/     → FluentValidation + attribute-based

Extensions/
  Abstractions/    → Page, PagedList, CurrencyValue, Quantity
  NanoId/          → NanoId generation
  RestEase/        → HTTP client via RestEase
  Sqids/           → Sqids encoding

Testing/
  NUnit/           → Test helpers: FakeLogger, TestingDefaults, metrics collectors

example/           → Reference service showing full usage
```

---

## Plugin System Architecture

### Core Interfaces

All interfaces live in `Hosting/Abstractions/src/Dosaic.Hosting.Abstractions/Plugins/`:

| Interface | Extends | Method |
|---|---|---|
| `IPluginActivateable` | — | Marker interface — all plugins must implement this |
| `IPluginServiceConfiguration` | `IPluginActivateable` | `ConfigureServices(IServiceCollection)` |
| `IPluginApplicationConfiguration` | `IPluginActivateable` | `ConfigureApplication(IApplicationBuilder)` |
| `IPluginEndpointsConfiguration` | `IPluginActivateable` | `ConfigureEndpoints(IEndpointRouteBuilder, IServiceProvider)` |
| `IPluginHealthChecksConfiguration` | `IPluginActivateable` | `ConfigureHealthChecks(IHealthChecksBuilder)` |
| `IPluginControllerConfiguration` | `IPluginActivateable` | `ConfigureControllers(IMvcBuilder)` |
| `IPluginConfigurator` | `IPluginActivateable` | Marker for sub-configurators injected as collections |

### Source Generator — Plugin Discovery

`Hosting/Generator/src/Dosaic.Hosting.Generator/PluginTypesGenerator.cs` is an `IIncrementalGenerator` that:

1. Scans the compilation for all **public, non-abstract** types implementing `IPluginActivateable` or having Dosaic attributes
2. Excludes Microsoft/System/Newtonsoft/FastEndpoints/NuGet/NSwag/FluentValidation namespaces
3. Emits `Dosaic.Generated.DosaicPluginTypes` with a `Type[] All` field
4. Enables AOT-compatible plugin discovery — no reflection at runtime

### TypeImplementationResolver

`Hosting/WebHost/src/Dosaic.Hosting.WebHost/Services/TypeImplementationResolver.cs` implements `IImplementationResolver`:

- Takes the `Type[]` from the generator + a default arguments dictionary
- Constructor injection: resolves parameters from defaults (`ILogger`, `IConfiguration`, `IHostEnvironment`, `ILoggerFactory`)
- Auto-resolves `[Configuration("section")]`-attributed types from `IConfiguration`
- Discovers `IPluginConfigurator` implementations across all assemblies and injects them as arrays
- Caches instances; `ClearInstances()` disposes them after build

### Orchestration Flow — PluginWebHostBuilder.Build()

```
1. HostConfigurator.Configure()
   ├── ConfigureAppConfiguration() — YAML + JSON layered config, env vars, CLI args
   ├── ConfigureWebHost()          — Kestrel: port from host:urls (default 8080), max body 8MB, no server header
   └── UseStructuredLogging()      — Serilog

2. ServiceConfigurator.Configure()
   ├── ConfigureDefaultServices()  — JSON serialization, compression, CORS, rate limiting, forwarded headers
   ├── ConfigureWebServices()      — MVC, controllers, model binding
   ├── ConfigureHealthChecks()     — Default liveness + [HealthCheck]-attributed + plugin health checks
   ├── ConfigureTelemetry()        — OpenTelemetry metrics, tracing, logging, Prometheus endpoint
   └── ConfigurePlugins()          — Iterates IPluginServiceConfiguration in sort order

3. webApplicationBuilder.Build()

4. AppConfigurator.Configure()
   ├── ConfigureWeb()              — Forwarded headers, compression, rate limiting, CORS, Prometheus, routing
   ├── ConfigureMiddlewares()      — Ordered by [Middleware] attribute
   ├── ConfigurePlugins()          — Iterates IPluginApplicationConfiguration in sort order
   └── ConfigureEndpoints()        — Health endpoints (/health/liveness, /health/readiness) + IPluginEndpointsConfiguration

5. ClearInstances()                — Dispose resolver instances
```

### Plugin Sort Order

Defined in `PluginActivateableExtensions.GetSortOrderForPlugin`:

| Origin | Sort Key | Execution |
|---|---|---|
| Dosaic-namespace plugins | `sbyte.MinValue` | First |
| Third-party plugins | `0` | Middle |
| Host-assembly plugins | `sbyte.MaxValue` | Last |

### Configuration Loading Order

1. `appsettings.*` files from root directory (`.json`, `.yaml`, `.yml`)
2. Files from additional paths via `HOST:ADDITIONALCONFIGPATHS` env var
3. Non-secrets files first, then `*.secrets.*` files
4. Command-line args
5. Environment variables

---

## Code Style

### Formatting

| Rule | Value |
|---|---|
| C# indent | 4 spaces |
| XML / JSON / YAML indent | 2 spaces |
| Charset | utf-8 (utf-8-bom for razor/cshtml) |
| Line endings | LF for YAML and shell, default otherwise |
| Trailing whitespace | trimmed |
| Final newline | yes |
| Multiple blank lines | disallowed (IDE2000 = warning) |

### C# Conventions

- **`var`** — always use `var` (all three `csharp_style_var_*` are suggestions)
- **Namespaces** — block-scoped (`csharp_style_namespace_declarations = block_scoped`)
- **Braces** — not required for single-line (`csharp_prefer_braces = false`)
- **`this.` qualifier** — avoid
- **Throw expressions** — disallowed
- **Nullable** — disabled globally (`<Nullable>disable</Nullable>`)
- **Implicit usings** — disabled; explicit global usings for: `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`
- **Modifier order** — `public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async`

### Naming

| Symbol | Convention | Example |
|---|---|---|
| Instance fields | `_camelCase` | `_logger` |
| Static fields | `_camelCase` | `_excludes` |
| Constants | PascalCase | `MaxRetries` |
| Locals / parameters | camelCase | `serviceCollection` |
| Local functions | PascalCase | `ResponseWriter` |
| Types, methods, properties | PascalCase | `ConfigureServices` |

### Key Diagnostics (enforced as warnings)

- CA1822 — make member static (private/internal API surface)
- CA2016 — forward CancellationToken
- IDE0005 — remove unnecessary usings
- IDE0044 — make field readonly
- IDE0051 — remove unused private members
- IDE0060 — remove unused parameters (non-public)
- IDE0161 — namespace declaration style
- IDE2000 — disallow multiple blank lines

---

## Testing

### Stack

| Package | Version | Purpose |
|---|---|---|
| NUnit | 4.5.0 | Test framework |
| NUnit3TestAdapter | 5.2.0 | **DO NOT upgrade to v6!** |
| AwesomeAssertions | 9.4.0 | Fluent assertions (`.Should()`) — **not** FluentAssertions |
| NSubstitute | 5.3.0 | Mocking — **not** Moq |
| Bogus | 35.6.5 | Fake data generation |
| AutoBogus | — | Convention-based fakes |
| WireMock.Net | 1.25.0 | HTTP server mocking |
| RichardSzalay.MockHttp | — | HttpClient mocking |
| TngTech.ArchUnitNET.NUnit | — | Architecture tests |
| Allure.NUnit | — | Test reporting |

### Test Conventions

1. **CamelCase** test method names — no underscores
2. **No** Arrange/Act/Assert comments
3. Test name must be self-explanatory
4. Use **AwesomeAssertions** for all assertions
5. Use **NSubstitute** for all mocking
6. Provide whole test class with setup when writing tests
7. Test projects are automatically `[Parallelizable(ParallelScope.Fixtures)]` and `[ExcludeFromCodeCoverage]`

### Test Project Naming

- Source: `Plugins/Mapping/Mapster/src/Dosaic.Plugins.Mapping.Mapster/`
- Tests: `Plugins/Mapping/Mapster/test/Dosaic.Plugins.Mapping.Mapster.Tests/`

### Test Helpers (Dosaic.Testing.NUnit)

- `FakeLogger<T>` — captures log entries, assert with `.Entries`
- `TestingDefaults` — standard test configuration
- `ActivityTestBootstrapper` — OpenTelemetry test setup
- `TestMetricsCollector` — metrics assertion helpers

### Coverage

- **80% line coverage threshold** enforced in CI
- Collected via `dotnet test --collect "Code Coverage"`

---

## Build & CI Commands

### Local Development

```bash
# Restore
dotnet restore

# Build
dotnet build ./Dosaic.sln

# Format check (CI uses this — must pass before merge)
dotnet format --verify-no-changes --no-restore

# Auto-fix formatting
dotnet format

# Run all tests
dotnet test ./Dosaic.sln

# Run tests with coverage
dotnet test --collect "Code Coverage;Format=Xml;CoverageFileName=coverage.xml" \
  --results-directory "./test-results" --no-restore --nologo -c Release --logger trx

# Run specific test project
dotnet test Plugins/Mapping/Mapster/test/Dosaic.Plugins.Mapping.Mapster.Tests

# Pack NuGet packages
dotnet pack -c Release
```

### Package Utilities

```bash
bash packages.sh -b   # Generate NuGet badge markdown
bash packages.sh -p   # Generate Directory.Packages.props entries
bash packages.sh -j   # Dump package list as JSON
bash packages.sh -n   # Dump package names
```

### CI Pipeline (GitHub Actions)

All workflows target **.NET 10.0.x** on **ubuntu-latest**.

| Workflow | Trigger | Steps |
|---|---|---|
| `dotnet-pr.yml` | PR to main | restore → format check → build Release → test+coverage → publish results → coverage ≥80% |
| `dotnet-main.yml` | Push to main | Same as PR + upload coverage artifact + job summary |
| `dotnet-release.yml` | GitHub release | restore → format → build → test → `dotnet pack` → `dotnet nuget push` to nuget.org |

---

## Creating a New Plugin

1. **Create directory structure** under the appropriate `Plugins/` category:
   ```
   Plugins/Category/MyPlugin/
     src/Dosaic.Plugins.Category.MyPlugin/
       Dosaic.Plugins.Category.MyPlugin.csproj
       MyPlugin.cs
     test/Dosaic.Plugins.Category.MyPlugin.Tests/
       Dosaic.Plugins.Category.MyPlugin.Tests.csproj
       MyPluginTests.cs
   ```

2. **Reference `Dosaic.Hosting.Abstractions`** in the `.csproj`:
   ```xml
   <ProjectReference Include="..\..\..\..\Hosting\Abstractions\src\Dosaic.Hosting.Abstractions\Dosaic.Hosting.Abstractions.csproj" />
   ```

3. **Implement one or more plugin interfaces**:
   ```csharp
   using Dosaic.Hosting.Abstractions.Plugins;
   using Dosaic.Hosting.Abstractions.Services;
   using Microsoft.Extensions.DependencyInjection;
   using Microsoft.Extensions.Logging;

   namespace Dosaic.Plugins.Category.MyPlugin
   {
       public class MyPlugin(IImplementationResolver implementationResolver, ILogger<MyPlugin> logger)
           : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
       {
           public void ConfigureServices(IServiceCollection serviceCollection)
           {
               // Register your services
           }

           public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
           {
               // Add health checks
           }
       }
   }
   ```

4. **Requirements for source generator discovery**:
   - Class must be `public`
   - Class must not be `abstract`
   - Must implement `IPluginActivateable` (directly or via `IPluginServiceConfiguration` etc.)
   - Namespace must not start with Microsoft/System/Newtonsoft etc.

5. **Available constructor dependencies** (auto-resolved by `TypeImplementationResolver`):
   - `ILogger<T>` / `ILogger`
   - `IConfiguration`
   - `IHostEnvironment`
   - `ILoggerFactory`
   - `IImplementationResolver`
   - Any `[Configuration("section")]`-attributed class
   - `IPluginConfigurator[]` / `IEnumerable<IPluginConfigurator>` subtypes

6. **Add the project to `Dosaic.sln`**:
   ```bash
   dotnet sln add Plugins/Category/MyPlugin/src/Dosaic.Plugins.Category.MyPlugin/Dosaic.Plugins.Category.MyPlugin.csproj
   dotnet sln add Plugins/Category/MyPlugin/test/Dosaic.Plugins.Category.MyPlugin.Tests/Dosaic.Plugins.Category.MyPlugin.Tests.csproj
   ```

---

## MCP Configuration

The repository includes MCP (Model Context Protocol) servers for AI-assisted development.

### `.vscode/mcp.json`

```jsonc
{
    "servers": {
        "context7": {
            "url": "https://mcp.context7.com/mcp",
            "type": "http"
        },
        "oraios/serena": {
            "type": "stdio",
            "command": "uvx",
            "args": [
                "--from",
                "git+https://github.com/oraios/serena",
                "serena",
                "start-mcp-server",
                "--open-web-dashboard",
                "false",
                "--context",
                "ide",
                "--project",
                "${workspaceFolder}"
            ]
        }
    }
}
```

### Server Descriptions

| Server | Type | Purpose |
|---|---|---|
| **context7** | HTTP | Documentation and library lookup — query up-to-date docs for any dependency |
| **oraios/serena** | stdio | Semantic code tools — symbol-level navigation, referencing, editing, project dashboard. Maintains `.serena/` project config and memory files for persistent context across sessions |

### Serena Usage Notes

- Serena provides symbol-based code navigation — prefer `find_symbol` and `get_symbols_overview` over reading entire files
- Use `find_referencing_symbols` to understand impact before editing
- Memory files persist across sessions — read existing memories before starting work
- Project config lives in `.serena/project.yml`

---

## Key File References

| File | Purpose |
|---|---|
| `global.json` | .NET SDK version (10.0.103) |
| `Directory.Build.props` | Shared build properties, global usings, test assembly attributes, NuGet metadata |
| `Directory.Packages.props` | Central package version management |
| `.editorconfig` | Code style rules, naming conventions, diagnostic severities |
| `Dosaic.sln` | Solution file |
| `Hosting/Abstractions/src/Dosaic.Hosting.Abstractions/Plugins/` | All plugin interfaces |
| `Hosting/Abstractions/src/Dosaic.Hosting.Abstractions/Services/IImplementationResolver.cs` | Plugin resolution contract |
| `Hosting/Abstractions/src/Dosaic.Hosting.Abstractions/Extensions/PluginActivateableExtensions.cs` | Plugin discovery + sort order |
| `Hosting/Generator/src/Dosaic.Hosting.Generator/PluginTypesGenerator.cs` | Source generator |
| `Hosting/WebHost/src/Dosaic.Hosting.WebHost/PluginWebHostBuilder.cs` | Main orchestrator |
| `Hosting/WebHost/src/Dosaic.Hosting.WebHost/Services/TypeImplementationResolver.cs` | Runtime type resolution |
| `Hosting/WebHost/src/Dosaic.Hosting.WebHost/Configurators/HostConfigurator.cs` | Config + Kestrel setup |
| `Hosting/WebHost/src/Dosaic.Hosting.WebHost/Configurators/ServiceConfigurator.cs` | DI + telemetry setup |
| `Hosting/WebHost/src/Dosaic.Hosting.WebHost/Configurators/AppConfigurator.cs` | Middleware + endpoint setup |
| `example/src/Dosaic.Example.Service/` | Reference implementation |
| `.vscode/mcp.json` | MCP server configuration |
| `.serena/project.yml` | Serena project configuration |
