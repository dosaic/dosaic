# Dosaic.Api.OpenApi

A Dosaic plugin that integrates [Swashbuckle / Swagger](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) into a Dosaic web service. It registers the Swagger generator, serves the interactive Swagger UI at `"/"`, auto-discovers XML documentation files, and ships several built-in filters that improve schema quality for value objects, read-only properties, and file-upload endpoints. Optional OAuth 2.0 / JWT bearer authentication can be wired up through configuration.

## Installation

```bash
dotnet add package Dosaic.Api.OpenApi
```

## Usage

### Registering the plugin

Add `OpenApiPlugin` to your host's plugin list. When using the source-generator based startup this happens automatically — the generator picks up `OpenApiPlugin` because it implements `IPluginActivateable`.

```csharp
using Dosaic.Hosting.WebHost;

PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

### Configuration

The plugin reads its settings from the `openapi` section of your application configuration (any supported format: JSON, YAML, environment variables).

**`appsettings.json` — minimal (no auth):**

```json
{
  "openapi": {}
}
```

**`appsettings.json` — with OAuth 2.0 / JWT bearer auth enabled:**

```json
{
  "openapi": {
    "auth": {
      "enabled": true,
      "tokenUrl": "https://auth.example.com/realms/my-realm/protocol/openid-connect/token",
      "authUrl": "https://auth.example.com/realms/my-realm/protocol/openid-connect/auth"
    }
  }
}
```

**`appsettings.yaml` equivalent:**

```yaml
openapi:
  auth:
    enabled: true
    tokenUrl: "https://auth.example.com/realms/my-realm/protocol/openid-connect/token"
    authUrl: "https://auth.example.com/realms/my-realm/protocol/openid-connect/auth"
```

### Annotating endpoints with Swashbuckle Annotations

The plugin enables [Swashbuckle.AspNetCore.Annotations](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/README.md#swashbuckleaspnetcoreannotations), so you can decorate your controllers and minimal-API handlers:

```csharp
using Swashbuckle.AspNetCore.Annotations;

[HttpGet("{id}")]
[SwaggerOperation(Summary = "Get an order by ID", Tags = ["Orders"])]
[SwaggerResponse(200, "Order found", typeof(OrderDto))]
[SwaggerResponse(404, "Order not found")]
public IActionResult GetOrder(Guid id) { ... }
```

### XML documentation

XML doc comments are picked up automatically. Enable XML output in your project file and the plugin will include every `*.xml` file found next to your assembly:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### File-upload endpoints

Endpoints with `IFormFile` parameters are automatically converted to a `multipart/form-data` request body schema — no extra attributes required:

```csharp
[HttpPost("upload")]
public IActionResult Upload(IFormFile file) { ... }
```

### Value-object properties

Properties (or types) decorated with `[Vogen.ValueObject]` are transparently flattened in the generated schema to their underlying primitive type. The `ValueObjectSchemaFilter` handles the schema rewrite, and the `ValueObjectDocumentFilter` cleans up any leftover component schemas, so the generated OpenAPI document is accurate and free of `$ref` noise.

### Read-only properties

Properties decorated with `[ReadOnly(true)]` from `System.ComponentModel` are marked `readOnly: true` in the generated schema:

```csharp
using System.ComponentModel;

public class OrderDto
{
    [ReadOnly(true)]
    public Guid Id { get; init; }

    public string Description { get; set; }
}
```

### Omitting models and fields from schema

Use `[OpenApiIgnore]` from `Dosaic.Api.OpenApi.Filters.Common` to omit elements from generated schemas.

- On a property: removes that property from the containing schema.
- On a type (`class` / `struct`): skips normal object expansion for that type.
- On an enum: omits enum value metadata and keeps only the underlying primitive schema type.
- On an enum member: removes that specific member from the generated enum value list.

```csharp
using Dosaic.Api.OpenApi.Filters.Common;

public class CreateOrderRequest
{
    public string Description { get; set; }

    [OpenApiIgnore]
    public string InternalNote { get; set; }
}
```

## Features

| Feature | Detail |
|---|---|
| **`OpenApiPlugin`** | Dosaic plugin implementing `IPluginServiceConfiguration`, `IPluginApplicationConfiguration`, and `IPluginEndpointsConfiguration` |
| **Swagger UI** | Served at the root path (`/`); displays request durations; auto-linked to `swagger/v1/swagger.json` |
| **SwaggerGen** | Registers `AddSwaggerGen` with the application name as document title and version `v1` |
| **XML comments** | All `*.xml` files next to the assembly are automatically included |
| **Swashbuckle Annotations** | `[SwaggerOperation]`, `[SwaggerResponse]`, `[SwaggerSchema]`, etc. enabled out of the box |
| **`ReadOnlyPropertySchemaFilter`** | Marks properties annotated with `[ReadOnly(true)]` as `readOnly: true` in the schema |
| **`OpenApiIgnoreSchemaFilter`** | Applies `[OpenApiIgnore]` rules to omit properties, selected enum members, or entire type details from generated schemas |
| **`ValueObjectSchemaFilter`** | Replaces Vogen value-object schemas with their underlying primitive type |
| **`ValueObjectDocumentFilter`** | Removes residual value-object component schemas after the schema filter rewrites inline $refs |
| **`FormFileFilter`** | Transforms `IFormFile` parameters into proper `multipart/form-data` request body schemas |
| **OAuth 2.0 / JWT auth** | Optional bearer security scheme supporting Authorization Code, Client Credentials, Password, and Implicit flows |

## Configuration reference

```csharp
// Bound from the "openapi" section
public class OpenApiConfiguration
{
    // Optional authentication settings
    public OpenApiAuthConfiguration Auth { get; set; }

    public class OpenApiAuthConfiguration
    {
        // Set to true to add the bearer security scheme and requirement
        public bool Enabled { get; set; }

        // Full token endpoint URL (used by Client Credentials, Password, and Authorization Code flows)
        public string TokenUrl { get; set; }

        // Full authorization endpoint URL (used by Implicit and Authorization Code flows)
        public string AuthUrl { get; set; }
    }
}
```


