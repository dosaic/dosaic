# Dosaic.Plugins.Endpoints.RestResourceEntity

A Dosaic plugin that provides a fluent, minimal-API builder for registering full CRUD REST endpoints for any entity that implements `IGuidIdentifier`. It wires up the standard five HTTP operations — list, get, create, update, and delete — against the CQRS handler interfaces from `Dosaic.Plugins.Handlers.Abstractions`, and automatically populates OpenAPI response metadata from a shared `GlobalResponseOptions` configuration.

## Installation

```bash
dotnet add package Dosaic.Plugins.Endpoints.RestResourceEntity
```

## Features

| Type | Description |
|---|---|
| `RestResourceEntityPlugin` | Dosaic plugin that registers `GlobalResponseOptions` as a singleton service |
| `GlobalResponseOptions` | Shared mapping of `HttpStatusCode → response Type` applied to every registered endpoint; preconfigured defaults detailed below |
| `RestSimpleResourceEndpointBuilder<T>` | Fluent builder returned by `AddSimpleRestResource<T>()` — registers one or more of the five standard routes |
| `RestSimpleResourceEndpointConfiguration` | Per-route configuration: policies, tags, produces, anonymous access, display/group names |
| `RestActions` | Static minimal-API delegate implementations for every CRUD operation |
| `RestSimpleResourceEntityEndpointExtensions` | `IEndpointRouteBuilder.AddSimpleRestResource<T>()` extension method |

### Default global responses

The following `GlobalResponseOptions` defaults are registered for every endpoint unless removed:

| Status Code | Response Type |
|---|---|
| 400 Bad Request | `ValidationErrorResponse` |
| 401 Unauthorized | `ErrorResponse` |
| 403 Forbidden | `ErrorResponse` |
| 500 Internal Server Error | `ErrorResponse` |

### Route table

| Method | Path | Handler interface | Default success code |
|---|---|---|---|
| `GET` | `/{resource}` | `IGetListHandler<T>` | 200 – `PagedList<T>` |
| `GET` | `/{resource}/{id:guid}` | `IGetHandler<T>` | 200 – `T` |
| `POST` | `/{resource}` | `ICreateHandler<T>` | 201 – `T` |
| `PUT` | `/{resource}/{id:guid}` | `IUpdateHandler<T>` | 200 – `T` |
| `DELETE` | `/{resource}/{id:guid}` | `IDeleteHandler<T>` | 204 – no content |

All routes require the `DEFAULT` authorization policy by default. Use `.AllowAnonymous()` to opt out on a per-route basis.

## Usage

### 1. Register the plugin

Add `RestResourceEntityPlugin` to your Dosaic application. Because Dosaic discovers plugins via its source generator, simply reference the package — the plugin is picked up automatically.

If you are building a standalone host without the source generator you can register it explicitly:

```csharp
// Program.cs
PluginWebHostBuilder.RunDefault(new[]
{
    typeof(RestResourceEntityPlugin),
    // ... other plugin types
});
```

### 2. Define your entity

Your entity must implement `IGuidIdentifier` (from `Dosaic.Plugins.Persistence.Abstractions`):

```csharp
using Dosaic.Plugins.Persistence.Abstractions;

public record Product : IGuidIdentifier
{
    public Guid Id { get; set; }
    public string Name { get; init; }
    public decimal Price { get; init; }
}
```

### 3. Implement CQRS handlers

Implement the handler interfaces for each operation you want to expose:

```csharp
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models;
using Dosaic.Extensions.Abstractions;

public class ProductHandlers :
    IGetListHandler<Product>,
    IGetHandler<Product>,
    ICreateHandler<Product>,
    IUpdateHandler<Product>,
    IDeleteHandler<Product>
{
    public Task<PagedList<Product>> GetListAsync(PagingRequest request, CancellationToken ct) { ... }
    public Task<Product> GetAsync(GuidIdentifier id, CancellationToken ct) { ... }
    public Task<Product> CreateAsync(Product model, CancellationToken ct) { ... }
    public Task<Product> UpdateAsync(Product model, CancellationToken ct) { ... }
    public Task DeleteAsync(GuidIdentifier id, CancellationToken ct) { ... }
}
```

Register the handlers with DI through your plugin's `ConfigureServices`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IGetListHandler<Product>, ProductHandlers>();
    services.AddScoped<IGetHandler<Product>, ProductHandlers>();
    services.AddScoped<ICreateHandler<Product>, ProductHandlers>();
    services.AddScoped<IUpdateHandler<Product>, ProductHandlers>();
    services.AddScoped<IDeleteHandler<Product>, ProductHandlers>();
}
```

### 4. Register the endpoints

Call `AddSimpleRestResource<T>()` inside your plugin's `ConfigureEndpoints` implementation:

```csharp
using Dosaic.Plugins.Endpoints.RestResourceEntity.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;

public class ProductPlugin : IPluginEndpointsConfiguration
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, IServiceProvider services)
    {
        // Register all five CRUD endpoints for /products
        endpoints.AddSimpleRestResource<Product>(services, "products")
                 .ForAll();
    }
}
```

#### Register only specific operations

```csharp
endpoints.AddSimpleRestResource<Product>(services, "products")
         .ForGetList()
         .ForGet()
         .ForPost();
```

#### Per-route configuration

Each `For*` method accepts an optional `Action<RestSimpleResourceEndpointConfiguration>` for fine-grained control:

```csharp
endpoints.AddSimpleRestResource<Product>(services, "products")
    .ForGetList(c => c.AllowAnonymous())
    .ForGet(c => c.AllowAnonymous())
    .ForPost(c => c
        .WithPolicies("products:write")
        .WithOpenApiTags("Products", "Management")
        .WithDisplayName("Create product")
        .WithGroupName("products"))
    .ForPut(c => c
        .WithPolicies("products:write")
        .Produces<ConflictErrorResponse>(HttpStatusCode.Conflict))
    .ForDelete(c => c
        .WithPolicies("products:delete")
        .DisableDefaultResponses());
```

### 5. Customise global response defaults

Override or extend the shared response metadata before the endpoints are built:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton(new GlobalResponseOptions()
        .Remove(HttpStatusCode.InternalServerError)   // remove a default
        .Add<ProblemDetails>(HttpStatusCode.UnprocessableContent) // add typed
        .Add(HttpStatusCode.ServiceUnavailable));       // add without body type
}
```

## Configuration

This plugin has no external configuration file settings. All behaviour is configured in code at endpoint registration time via `GlobalResponseOptions` and `RestSimpleResourceEndpointConfiguration`.

### `RestSimpleResourceEndpointConfiguration` API

| Method | Description |
|---|---|
| `.WithOpenApiTags(params string[] tags)` | Override OpenAPI tags (defaults to the resource name) |
| `.WithPolicies(params string[] policies)` | Add authorization policy names (always includes `"DEFAULT"`) |
| `.AllowAnonymous()` | Disable authorization on this route |
| `.WithDisplayName(string displayName)` | Set the endpoint display name |
| `.WithGroupName(string groupName)` | Set the OpenAPI group name |
| `.Produces<T>(HttpStatusCode statusCode)` | Add a custom response type for a specific status code |
| `.DisableDefaultResponses()` | Skip auto-registration of success/4xx response metadata |

### `GlobalResponseOptions` API

| Method | Description |
|---|---|
| `.Add<T>(HttpStatusCode)` | Add a typed default response |
| `.Add(HttpStatusCode)` | Add a status-code-only default response (no body type) |
| `.Add(HttpStatusCode, Type)` | Add a default response with an explicit type |
| `.Remove(HttpStatusCode)` | Remove an existing default response |
