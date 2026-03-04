# Dosaic.Hosting.Abstractions

Dosaic.Hosting.Abstractions is the `core abstraction/interface package` that allows `.NET developers` to `build their own Dosaic plugins`. Every plugin in the Dosaic ecosystem depends on this package.

## Installation

```shell
dotnet add package Dosaic.Hosting.Abstractions
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Hosting.Abstractions" Version="" />
```

## Usage

### Creating a Plugin

A plugin is any `public`, non-`abstract` class that implements one or more of the plugin interfaces below. The source generator in `Dosaic.Hosting.Generator` automatically discovers all such classes at compile time — no runtime reflection or manual registration needed.

```csharp
using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace MyCompany.MyPlugin
{
    public class MyPlugin : IPluginServiceConfiguration, IPluginApplicationConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMyService, MyService>();
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseMyMiddleware();
        }
    }
}
```

---

## Plugin Interfaces

All plugin interfaces live in `Dosaic.Hosting.Abstractions.Plugins` and extend `IPluginActivateable`.

| Interface | Method | Purpose |
|---|---|---|
| `IPluginActivateable` | — | Marker interface — **all plugins must implement this** (directly or via one of the interfaces below) |
| `IPluginServiceConfiguration` | `ConfigureServices(IServiceCollection)` | DI registrations, called during the service-configuration phase |
| `IPluginApplicationConfiguration` | `ConfigureApplication(IApplicationBuilder)` | Middleware pipeline setup, called after `Build()` |
| `IPluginEndpointsConfiguration` | `ConfigureEndpoints(IEndpointRouteBuilder, IServiceProvider)` | Minimal API endpoint registration |
| `IPluginHealthChecksConfiguration` | `ConfigureHealthChecks(IHealthChecksBuilder)` | Custom health check registration |
| `IPluginControllerConfiguration` | `ConfigureControllers(IMvcBuilder)` | MVC/controller configuration |
| `IPluginConfigurator` | — | Marker for sub-configurator objects injected as `IPluginConfigurator[]` collections into plugins |

### Plugin execution order

| Plugin namespace | Execution order |
|---|---|
| `Dosaic.*` namespace | First (`sbyte.MinValue`) |
| Third-party plugins | Middle (`0`) |
| Host assembly plugins | Last (`sbyte.MaxValue`) |

### IPluginServiceConfiguration

Called during service-collection setup. Use this for DI registrations.

```csharp
public class MyPlugin : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IMyService, MyService>();
        serviceCollection.AddSingleton<MyOptions>();
    }
}
```

### IPluginApplicationConfiguration

Called after `WebApplication.Build()`. Use this for `UseXyz()` pipeline calls.

```csharp
public class MyPlugin : IPluginApplicationConfiguration
{
    public void ConfigureApplication(IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseRouting();
        applicationBuilder.UseAuthentication();
    }
}
```

### IPluginEndpointsConfiguration

Registers minimal API endpoints. Called during endpoint routing setup.

```csharp
public class MyPlugin : IPluginEndpointsConfiguration
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
    {
        endpointRouteBuilder.MapGet("/hello", () => "Hello, World!")
            .RequireAuthorization();
    }
}
```

### IPluginHealthChecksConfiguration

Registers health checks programmatically. See also the health check attributes for declarative registration.

```csharp
public class MyPlugin : IPluginHealthChecksConfiguration
{
    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
    {
        healthChecksBuilder.AddUrlGroup(
            new Uri("https://my-dependency/health"),
            "my-dependency",
            HealthStatus.Unhealthy,
            new[] { HealthCheckTag.Readiness });
    }
}
```

### IPluginControllerConfiguration

Configures the MVC builder (e.g. options, formatters, assemblies).

```csharp
public class MyPlugin : IPluginControllerConfiguration
{
    public void ConfigureControllers(IMvcBuilder controllerBuilder)
    {
        controllerBuilder.AddMvcOptions(options => options.RespectBrowserAcceptHeader = true);
        controllerBuilder.AddApplicationPart(typeof(MyPlugin).Assembly);
    }
}
```

### IPluginConfigurator

A sub-configurator is a small, focused configuration object that is automatically collected and injected as an array into plugins that declare a constructor parameter of type `IPluginConfigurator[]` (or `IEnumerable<IPluginConfigurator>`).

```csharp
// Sub-configurator defined anywhere in the application
public class MyFeatureConfigurator : IPluginConfigurator
{
    public string ConnectionString { get; set; } = "...";
}

// Plugin that receives all configurators
public class MyPlugin : IPluginServiceConfiguration
{
    private readonly MyFeatureConfigurator[] _configurators;

    public MyPlugin(MyFeatureConfigurator[] configurators)
    {
        _configurators = configurators;
    }

    public void ConfigureServices(IServiceCollection serviceCollection) { ... }
}
```

---

## Attributes

### `[Configuration]`

Decorating a class with `[Configuration("section")]` causes the web host to automatically bind the class to the corresponding section in `appsettings.*` files or environment variables. The bound instance is then injectable as a constructor parameter in any plugin.

```yaml
# appsettings.yaml
myFeature:
  connectionString: "Server=localhost"
  timeout: 30
```

```csharp
using Dosaic.Hosting.Abstractions.Attributes;

[Configuration("myFeature")]
public class MyFeatureConfiguration
{
    public string ConnectionString { get; set; }
    public int Timeout { get; set; }
}

// Consume in any plugin via constructor injection
public class MyPlugin : IPluginServiceConfiguration
{
    private readonly MyFeatureConfiguration _config;

    public MyPlugin(MyFeatureConfiguration config)
    {
        _config = config;
    }

    public void ConfigureServices(IServiceCollection serviceCollection) { ... }
}
```

Nested sections are supported using `:` as the delimiter:

```csharp
[Configuration("database:primary")]
public class PrimaryDbConfig { ... }
```

### `[Middleware]`

Marks an `ApiMiddleware` subclass for automatic registration in the middleware pipeline. The optional `order` parameter controls execution order (default: `int.MaxValue` — runs last). Lower values run earlier.

```csharp
using Dosaic.Hosting.Abstractions.Attributes;

[Middleware(order: 100)]
public class MyMiddleware : ApiMiddleware
{
    public MyMiddleware(RequestDelegate next, IDateTimeProvider dateTimeProvider)
        : base(next, dateTimeProvider) { }

    public override async Task Invoke(HttpContext context)
    {
        // before
        await Next.Invoke(context);
        // after
    }
}
```

Built-in middlewares and their order:

| Middleware | Order | Behaviour |
|---|---|---|
| `ExceptionMiddleware` | `int.MinValue` (first) | Catches all exceptions; maps `DosaicException` subtypes to HTTP status codes |
| `EnrichRequestMetricsMiddleware` | `int.MaxValue` (last) | Adds `azp` claim as a metrics tag on every request |
| `RequestContentLengthLimitMiddleware` | `int.MaxValue` (last) | Returns `413` when `Content-Length` exceeds the Kestrel limit |

### `[ReadinessCheckAttribute]`

Registers an `IHealthCheck` implementation as a **readiness** health check (tags it with `HealthCheckTag.Readiness`).

```csharp
using Dosaic.Hosting.Abstractions.Attributes;

[ReadinessCheck("database")]
public class DatabaseHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // check connectivity ...
        return HealthCheckResult.Healthy("Database is reachable.");
    }
}
```

### `[LivenessCheckAttribute]`

Registers an `IHealthCheck` implementation as a **liveness** health check (tags it with `HealthCheckTag.Liveness`).

```csharp
[LivenessCheck("application")]
public class ApplicationLivenessCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy());
}
```

Both attributes can be combined on the same class, and additional tags can be passed via the overloaded constructors:

```csharp
[ReadinessCheck("kafka")]
[LivenessCheck("kafka")]
public class KafkaHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // check Kafka connectivity
        return Task.FromResult(HealthCheckResult.Healthy("Kafka is available."));
    }
}
```

### `[YamlTypeConverterAttribute]`

Attaches a custom YAML type converter to a class or struct so that it is used during `SerializationExtensions.Serialize` / `Deserialize` calls with `SerializationMethod.Yaml`. The converter type must implement `IYamlConverter`.

```csharp
using Dosaic.Hosting.Abstractions.Attributes;

[YamlTypeConverter(typeof(MyTypeConverter))]
public class MySpecialType { ... }

public class MyTypeConverter : IYamlConverter
{
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) { ... }
    public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer) { ... }
}
```

---

## HealthCheckTag

`HealthCheckTag` is a validated value object (via [Vogen](https://github.com/SteveDunn/Vogen)) with two predefined instances:

| Instance | Value |
|---|---|
| `HealthCheckTag.Readiness` | `"readiness"` |
| `HealthCheckTag.Liveness` | `"liveness"` |

Custom tags are supported:

```csharp
var tag = HealthCheckTag.From("custom-tag");
```

---

## Exceptions

All domain exceptions extend `DosaicException`, which carries an `HttpStatus` property automatically used by `ExceptionMiddleware` to produce the correct HTTP response.

| Exception | Default HTTP status |
|---|---|
| `DosaicException` | `500 Internal Server Error` (configurable via constructor) |
| `NotFoundDosaicException` | `404 Not Found` |
| `ConflictDosaicException` | `409 Conflict` |
| `ValidationDosaicException` | `422 Unprocessable Entity` |

```csharp
using Dosaic.Hosting.Abstractions.Exceptions;

// Simple usage
throw new NotFoundDosaicException("Order", orderId);
// → message: "Could not find Order with id '42'"

throw new ConflictDosaicException("An order with this reference already exists.");

throw new ValidationDosaicException("Validation failed", new List<FieldValidationError>
{
    new("email", "must be a valid email address"),
    new("amount", "must be greater than 0")
});

// Throw with a custom HTTP status
throw new DosaicException("Service unavailable.", StatusCodes.Status503ServiceUnavailable);
```

### Error Response Models

`ExceptionMiddleware` produces JSON responses using these models from `Dosaic.Hosting.Abstractions.Middlewares.Models`:

```csharp
// Standard error response
record ErrorResponse(DateTime Timestamp, string Message, string RequestId);

// Validation error response (extends ErrorResponse)
record ValidationErrorResponse : ErrorResponse
{
    IEnumerable<FieldValidationError> ValidationErrors { get; }
}

// Single field validation error
record FieldValidationError(string Field, string ValidationMessage);
```

---

## IImplementationResolver

`IImplementationResolver` is the runtime service used internally by the web host to discover and instantiate plugins. It is also injectable into plugins for advanced scenarios.

```csharp
public interface IImplementationResolver
{
    List<Type> FindTypes();
    List<Assembly> FindAssemblies();
    object ResolveInstance(Type type);
    void ClearInstances();
}
```

Convenience extensions from `ImplementationResolverExtensions`:

```csharp
// Find all types matching a predicate
List<Type> types = resolver.FindTypes(t => t.HasAttribute<MyAttribute>());

// Find and instantiate all types matching a predicate
List<object> instances = resolver.FindAndResolve(t => t.Implements<IMyService>());

// Find and instantiate all implementations of T
List<IMyService> services = resolver.FindAndResolve<IMyService>();
```

---

## IFactory\<T\>

`IFactory<TService>` is a lightweight factory abstraction for resolving services lazily or optionally from the DI container. Register using `AddFactory<T>()`.

```csharp
using Dosaic.Hosting.Abstractions.DependencyInjection;
using Dosaic.Hosting.Abstractions.Extensions;

// Registration
serviceCollection.AddFactory<IMyService>();

// Injection and usage
public class MyConsumer
{
    private readonly IFactory<IMyService> _factory;

    public MyConsumer(IFactory<IMyService> factory) => _factory = factory;

    public void DoWork()
    {
        var service = _factory.Create();             // throws if not registered
        var serviceOrNull = _factory.CreateOrNull(); // returns null if not registered
    }
}
```

---

## Metrics

Dosaic uses OpenTelemetry for metrics. The static `Metrics` class provides typed factory methods that cache instrument instances, preventing duplicate-creation errors.

```csharp
using Dosaic.Hosting.Abstractions.Metrics;

// Counter
var counter = Metrics.CreateCounter<long>("my_requests_total", "calls", "Total number of requests");
counter.Add(1);
counter.Add(1, new KeyValuePair<string, object>("status", "ok"));

// Histogram
var histogram = Metrics.CreateHistogram<double>("my_request_duration_seconds", "s", "Request duration");
histogram.Record(0.42);

// Observable gauge (value polled by the metrics exporter)
Metrics.CreateObservableGauge("queue_depth", () => GetQueueDepth(), "items", "Current queue depth");

// Observable counter
Metrics.CreateObservableCounter("processed_total", () => GetProcessedCount(), "items", "Total processed");
```

Access the underlying `Meter` directly for advanced scenarios:

```csharp
var upDownCounter = Metrics.Meter.CreateUpDownCounter<int>("active_connections", "connections");
```

---

## Tracing

### DosaicDiagnostic

`DosaicDiagnostic.CreateSource()` creates an `ActivitySource` named after the **calling class's fully-qualified name** using the call stack. Call it from a `static` field initializer.

```csharp
using Dosaic.Hosting.Abstractions;
using System.Diagnostics;

public class MyService
{
    private static readonly ActivitySource _activitySource = DosaicDiagnostic.CreateSource();

    public async Task DoWorkAsync()
    {
        using var activity = _activitySource.StartActivity("DoWork");
        // ... work ...
    }
}
```

Constants:

| Constant | Value | Purpose |
|---|---|---|
| `DosaicDiagnostic.DosaicActivityPrefix` | `"Dosaic."` | Prefix for all Dosaic activity sources |
| `DosaicDiagnostic.DosaicAllActivities` | `"Dosaic.*"` | Wildcard for subscribing to all Dosaic traces in OpenTelemetry |

### TracingExtensions

```csharp
using Dosaic.Hosting.Abstractions.Extensions;

// Wraps an async call and auto-sets Ok/Error activity status
var result = await _activitySource.TrackStatusAsync(async activity =>
{
    activity?.SetTag("input", input);
    return await _service.ProcessAsync(input);
});

// Set tags in bulk with an optional prefix
activity.SetTags(new Dictionary<string, string> { ["key"] = "value" }, prefix: "app.");

// Manually set status
activity.SetOkStatus();
activity.SetErrorStatus(exception);
```

---

## Serialization

`SerializationExtensions` provides uniform JSON and YAML serialization.

```csharp
using Dosaic.Hosting.Abstractions.Extensions;

var obj = new MyDto { Name = "hello" };

// JSON (default)
string json = obj.Serialize();
MyDto dto = json.Deserialize<MyDto>();

// YAML
string yaml = obj.Serialize(SerializationMethod.Yaml);
MyDto dto2 = yaml.Deserialize<MyDto>(SerializationMethod.Yaml);

// Access the default JsonSerializerOptions (camelCase, enums as strings, etc.)
var options = SerializationExtensions.DefaultOptions;
```

### IKindSpecifier — Polymorphic deserialization

Implementing `IKindSpecifier` on an interface enables discriminated-union deserialization where the concrete type is selected based on a `Kind` property in the JSON/YAML payload.

```csharp
public interface IShape : IKindSpecifier { }

public class Circle : IShape
{
    public string Kind => "circle";
    public double Radius { get; set; }
}

public class Rectangle : IShape
{
    public string Kind => "rectangle";
    public double Width { get; set; }
    public double Height { get; set; }
}

// Deserializes to Circle or Rectangle depending on the "kind" field
IShape shape = """{"kind":"circle","radius":5}""".Deserialize<IShape>();
```

---

## Utility Extensions

### ObjectExtensions — DeepPatch

Applies non-null property values from a `patch` object onto a `target`, with configurable behaviour for nested objects and lists.

```csharp
using Dosaic.Hosting.Abstractions.Extensions;

var original = new MyEntity { Name = "Old", Tags = new List<string> { "a" } };
var patch    = new MyEntity { Name = "New", Tags = new List<string> { "b" } };

original.DeepPatch(patch);
// original.Name == "New", original.Tags == ["a", "b"]   (new items are merged)

original.DeepPatch(patch, PatchMode.OverwriteLists);
// original.Tags == ["b"]                                 (list is replaced)
```

`PatchMode` flags: `Full`, `IgnoreLists`, `IgnoreObjects`, `OverwriteLists`.

### StringExtensions

```csharp
"MyPropertyName".ToSnakeCase();     // "my_property_name"
"hello world".ToUrlEncoded();       // "hello%20world"
"hello%20world".FromUrlEncoded();   // "hello world"
```

### ConfigurationExtensions

```csharp
// Bind a config section to a new instance of T
var opts = configuration.BindToSection<MyOptions>("myFeature");
```

### TypeExtensions

```csharp
typeof(MyClass).Implements<IMyInterface>()   // true / false
typeof(MyClass).HasAttribute<MyAttribute>()  // true / false
typeof(MyClass).GetAttribute<MyAttribute>()  // attribute instance or null
typeof(MyClass).CanBeInstantiated()          // !IsAbstract && !IsInterface
typeof(MyClass).GetNormalizedName()          // human-readable generic type name
```

### EnumerableExtensions

```csharp
items.ForEach(item => Console.WriteLine(item));
```

### EnumExtensions

```csharp
PatchMode.IgnoreLists.IsFlagSet(PatchMode.IgnoreLists); // true
```

---

## GlobalStatusCodeOptions

Controls which HTTP status codes are automatically rewritten to the standard `ErrorResponse` JSON format by `ExceptionMiddleware`. Default codes: `401`, `403`, `404`, `406`, `415`, `500`.

```csharp
// Customise in a plugin's ConfigureServices:
serviceCollection.Configure<GlobalStatusCodeOptions>(opts =>
{
    opts.Add(HttpStatusCode.ServiceUnavailable);
    opts.Remove(HttpStatusCode.NotFound);
    opts.Clear(); // remove all defaults
});
```

---

## ApiMiddleware

`ApiMiddleware` is the abstract base class for all Dosaic middlewares. Subclass it and decorate with `[Middleware]` for automatic registration in the pipeline.

```csharp
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Attributes;
using Chronos.Abstractions;

[Middleware(order: 50)]
public class CorrelationIdMiddleware : ApiMiddleware
{
    public CorrelationIdMiddleware(RequestDelegate next, IDateTimeProvider dateTimeProvider)
        : base(next, dateTimeProvider) { }

    public override async Task Invoke(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out _))
            context.Response.Headers.Append("X-Correlation-Id", Guid.NewGuid().ToString());

        await Next.Invoke(context);
    }
}
```

Helper methods available inside `ApiMiddleware`:

```csharp
// Write a typed JSON response
await WriteResponse(context, StatusCodes.Status200OK, new { ok = true });

// Write the standard ErrorResponse JSON
await WriteDefaultResponse(context, StatusCodes.Status400BadRequest, "Custom message");
```
