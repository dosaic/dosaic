# Dosaic Example Service

A reference implementation demonstrating how to build a web service using the Dosaic plugin-first .NET framework. It shows plugin registration, minimal API endpoints, MVC controllers, background jobs, OpenTelemetry tracing, IP rate limiting, and layered configuration — all wired together with a single entry-point call.

## Running the Example

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker & Docker Compose (optional, for containerised run)

### Run locally

```bash
# From the repository root
dotnet run --project example/src/Dosaic.Example.Service
```

The service listens on `http://localhost:5300` by default (configured in `appsettings.yml`).

### Run with Docker Compose

```bash
# From the example/ directory
docker compose up --build
```

The container exposes port **8080** on the host.

### Build & publish manually

```bash
dotnet publish example/src/Dosaic.Example.Service \
  -c Release -o example/src/Dosaic.Example.Service/bin/Release/net10.0/publish
```

## Plugins Used

| Plugin / Package | Purpose |
|---|---|
| `Dosaic.Hosting.WebHost` | Core host builder — `PluginWebHostBuilder.RunDefault()` |
| `Dosaic.Hosting.Generator` | Source generator that emits `DosaicPluginTypes.All` at compile time |
| `Dosaic.Api.OpenApi` | OpenAPI / Swagger UI with optional Keycloak OAuth2 flow |
| `Dosaic.Plugins.Jobs.Hangfire` | Recurring background jobs via Hangfire with in-memory storage |

Built-in framework features also exercised:

- **OpenTelemetry** tracing (custom `ActivitySource` on endpoints)
- **IP rate limiting** (AspNetCoreRateLimit configuration)
- **Serilog** structured logging
- **Vogen** strongly-typed value objects

## Project Structure

```
example/
├── docker-compose.yaml                         # Docker Compose service definition
├── Dockerfile                                  # Multi-stage container image
└── src/
    └── Dosaic.Example.Service/
        ├── Program.cs                          # Entry point — one line
        ├── ExampleWebHost.cs                   # Plugin: minimal API endpoints + service registration
        ├── TestController.cs                   # MVC controller with Vogen value objects
        ├── TestHangfire.cs                     # Hangfire configurator + recurring jobs
        ├── appsettings.yml                     # Host URL, OpenAPI auth, Hangfire queues
        ├── appsettings.logging.yaml            # Serilog minimum levels
        ├── appsettings.ip-rate-limiting.json   # IP rate-limit rules and policies
        └── Dosaic.Example.Service.csproj       # Project file with plugin references
```

## Usage

### Entry point

The entire host is bootstrapped in a single line. The source generator produces `DosaicPluginTypes.All`, so no runtime assembly scanning is needed:

```csharp
// Program.cs
using Dosaic.Hosting.WebHost;

PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

### Implementing a plugin

`ExampleWebHost` implements two plugin interfaces — `IPluginServiceConfiguration` to register services and `IPluginEndpointsConfiguration` to map minimal API routes. Constructor parameters are resolved automatically by `TypeImplementationResolver`:

```csharp
public class ExampleWebHost : IPluginEndpointsConfiguration, IPluginServiceConfiguration
{
    public ExampleWebHost(IImplementationResolver implementationResolver, ILogger logger) { ... }

    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        // enumerate all discovered plugins at startup
        var plugins = _implementationResolver.FindAndResolve<IPluginActivateable>();
        _logger.LogDebug("Found {ItemCount} plugins", plugins.Count);
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
    {
        endpointRouteBuilder.MapGet("/hello", () => "Hello, World!");

        // RequireAuthorization() integrates with the configured auth provider
        endpointRouteBuilder.MapGet("/secure", () => "Hello, secure World!").RequireAuthorization();
    }
}
```

### OpenTelemetry tracing

Endpoints create custom spans via `ActivitySource`:

```csharp
var myActivitySource = new ActivitySource("WebHostSamplePlugin");

endpointRouteBuilder.MapGet("/hello", () =>
{
    using var activity = myActivitySource.StartActivity("SayHello");
    activity?.SetTag("foo", 1);
    activity?.SetTag("bar", "Hello, World!");
    return "Hello, World!";
});
```

To export traces/metrics/logs with a custom OpenTelemetry service name, configure `telemetry:name` together with `telemetry:endpoint` in your app settings.

### MVC controller with value objects

`TestController` shows a standard `[ApiController]` alongside [Vogen](https://github.com/SteveDunn/Vogen) strongly-typed value objects and Swashbuckle response annotations:

```csharp
[ApiController, Route("test")]
public class TestController : ControllerBase
{
    [HttpPost]
    [SwaggerResponse(200, "the manipulated object", typeof(Entry))]
    public Entry Create([FromBody] Entry entry, [FromQuery] EntryId idToSet)
    {
        entry.EntryId = idToSet;
        return entry;
    }
}

[ValueObject<int>]
public partial class EntryId
{
    private static Validation Validate(int input) =>
        input < 1 ? Validation.Invalid("lower as one") : Validation.Ok;
}
```

### Background jobs with Hangfire

`TestHangfire` implements `IHangfireConfigurator` to configure in-memory storage. Jobs decorated with `[RecurringJob]` are scheduled automatically:

```csharp
public class TestHangfire : IHangfireConfigurator
{
    public bool IncludesStorage => false;

    public void Configure(IGlobalConfiguration config) => config.UseMemoryStorage();

    public void ConfigureServer(BackgroundJobServerOptions options) => options.WorkerCount = 1;
}

[RecurringJob("*/1 * * * *")]
public class TestJob : AsyncJob
{
    public TestJob(ILogger logger) : base(logger) { }

    protected override Task<object> ExecuteJobAsync(CancellationToken cancellationToken)
        => Task.FromResult<object>(new { Hello = "World" });
}
```

### Layered configuration

Dosaic loads configuration files in order — plain files first, then `*.secrets.*` files. The example splits settings across three files:

| File | Contents |
|---|---|
| `appsettings.yml` | Host URL (`http://+:5300`), OpenAPI auth URLs, Hangfire queue names |
| `appsettings.logging.yaml` | Serilog minimum log levels |
| `appsettings.ip-rate-limiting.json` | IP rate-limit rules, endpoint/client whitelists, policies |
