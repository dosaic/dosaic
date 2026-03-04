# Plugin System Architecture

## Core Interfaces (Hosting/Abstractions/src/.../Plugins/)

| Interface | Extends | Method |
|---|---|---|
| `IPluginActivateable` | — | Marker interface, all plugins implement this |
| `IPluginServiceConfiguration` | `IPluginActivateable` | `ConfigureServices(IServiceCollection)` |
| `IPluginApplicationConfiguration` | `IPluginActivateable` | `ConfigureApplication(IApplicationBuilder)` |
| `IPluginEndpointsConfiguration` | `IPluginActivateable` | `ConfigureEndpoints(IEndpointRouteBuilder, IServiceProvider)` |
| `IPluginHealthChecksConfiguration` | `IPluginActivateable` | `ConfigureHealthChecks(IHealthChecksBuilder)` |
| `IPluginControllerConfiguration` | `IPluginActivateable` | `ConfigureControllers(IMvcBuilder)` |
| `IPluginConfigurator` | `IPluginActivateable` | Marker for sub-configurators injected as collections |

## Source Generator — PluginTypesGenerator

`Hosting/Generator/src/Dosaic.Hosting.Generator/PluginTypesGenerator.cs`
- `IIncrementalGenerator` scans compilation for all public, non-abstract types implementing `IPluginActivateable` or having Dosaic attributes
- Excludes Microsoft/System/Newtonsoft/etc namespaces
- Emits `Dosaic.Generated.DosaicPluginTypes` with `Type[] All` field
- Enables AOT-compatible discovery (no reflection scanning at runtime)

## TypeImplementationResolver (Hosting/WebHost/src/.../Services/)

- Implements `IImplementationResolver`
- Takes the `Type[]` from the generator + default arguments dict
- Constructor injection: resolves parameters from `_defaultArguments` (ILogger, IConfiguration, IHostEnvironment, etc.)
- Auto-resolves `[Configuration]`-attributed types from `IConfiguration`
- Discovers `IPluginConfigurator` implementations and injects them as arrays
- Caches instances, supports `ClearInstances()` for disposal

## Orchestration Flow — PluginWebHostBuilder.Build()

1. **HostConfigurator.Configure()** — configures app configuration (YAML/JSON), Serilog, Kestrel
2. **ServiceConfigurator.Configure()** — in order:
   a. `ConfigureDefaultServices()` — JSON, compression, CORS, rate limiting, forwarded headers
   b. `ConfigureWebServices()` — MVC, controllers, model binding
   c. `ConfigureHealthChecks()` — default liveness checks + attribute-discovered + plugin health checks
   d. `ConfigureTelemetry()` — OpenTelemetry metrics, tracing, logging
   e. `ConfigurePlugins()` — iterates `IPluginServiceConfiguration` plugins
3. **host = webApplicationBuilder.Build()**
4. **AppConfigurator.Configure()** — in order:
   a. `ConfigureWeb()` — forwarded headers, compression, rate limiting, CORS, Prometheus, routing
   b. `ConfigureMiddlewares()` — ordered by `[Middleware]` attribute
   c. `ConfigurePlugins()` — iterates `IPluginApplicationConfiguration` plugins
   d. `ConfigureEndpoints()` — health endpoints + iterates `IPluginEndpointsConfiguration` plugins
5. **ClearInstances()** — dispose resolver instances

## Plugin Sort Order

Defined in `PluginActivateableExtensions.GetSortOrderForPlugin`:
- **Dosaic-namespace plugins** → `sbyte.MinValue` (first)
- **Third-party plugins** → `0`
- **Host-assembly plugins** → `sbyte.MaxValue` (last)

## Plugin Implementation Pattern

```csharp
public class MyPlugin(IImplementationResolver resolver, ILogger<MyPlugin> logger) 
    : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection services) { /* register DI */ }
    public void ConfigureHealthChecks(IHealthChecksBuilder builder) { /* add checks */ }
}
```

Dependencies are injected via constructor: ILogger, IConfiguration, IHostEnvironment, IImplementationResolver, 
[Configuration]-attributed types, and IPluginConfigurator[] collections.
