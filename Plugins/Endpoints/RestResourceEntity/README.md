# Dosaic.Plugins.Endpoints.RestResourceEntity

## Overview
A plugin that provides REST endpoint configurations for resource entities in the Dosaic framework. This plugin simplifies the creation of standardized REST endpoints for entities that implement `IGuidIdentifier`.

## Features
- Simplified REST endpoint configuration
- Support for standard HTTP methods
- Built-in response type management
- Configurable OpenAPI documentation
- Flexible authorization policies
- Global response handling

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Endpoints.RestResourceEntity
```

or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Endpoints.RestResourceEntity" Version="" />
```
## Usage
Configure REST endpoints for your resource entities:

```csharp
public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
{
    // to configure all http verbs/actions at once
    endpointRouteBuilder
        .AddSimpleRestResource<Entity>(serviceProvider, "api-route-resource-name")
        .ForAll();

    //to configure specific http verbs/actions
    endpointRouteBuilder
        .AddSimpleRestResource<Entity>(serviceProvider, "api-route-resource-name")
        .ForDelete(c =>
            c.WithDisplayName("deleter")
             .WithGroupName("testgorup")
             .WithOpenApiTags("test", "tester")
             .WithPolicies("deleter") // or .AllowAnonymous()
             .Produces<ErrorResponse>(HttpStatusCode.Accepted)
             .Produces<ErrorResponse>(HttpStatusCode.OK)
             .DisableDefaultResponses()
    );
}
```

## Configuration Options
- OpenAPI tags customization
- Authorization policies
- Custom response types
- Default response handling
- Anonymous access control

## Global Response Types
The plugin includes default response mappings for common HTTP status codes:
- 401 Unauthorized: `ErrorResponse`
- 403 Forbidden: `ErrorResponse`
- 500 Internal Server Error: `ErrorResponse`
- 400 Bad Request: `ValidationErrorResponse`

Default response mapping can be customized
```csharp
public sealed class RestResourceEntityPlugin : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        var options = new GlobalResponseOptions();
        options.Add(HttpStatusCode.NoContent);
        options.Add<GlobalResponseOptionsTests>(HttpStatusCode.Processing);
        options.Remove(HttpStatusCode.InternalServerError);
        serviceCollection.AddSingleton<GlobalResponseOptions>(options);
    }
}
```

