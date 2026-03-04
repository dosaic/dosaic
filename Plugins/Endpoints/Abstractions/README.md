# Dosaic.Plugins.Endpoints.Abstractions

Provides core abstractions for building REST endpoint resources in the Dosaic framework. Currently exposes the `ResourceIdentifierAttribute` — a property-level marker that designates which property on a resource model serves as its primary identifier in REST routing.

This package is a low-level dependency consumed by higher-level endpoint packages such as `Dosaic.Plugins.Endpoints.RestResourceEntity`.

## Installation

```shell
dotnet add package Dosaic.Plugins.Endpoints.Abstractions
```

## Features

| Type | Kind | Description |
|------|------|-------------|
| `ResourceIdentifierAttribute` | `Attribute` | Marks a property as the resource's primary identifier for REST endpoint routing. Applied to properties (`AttributeTargets.Property`). |

## Usage

### Marking a resource identifier

Apply `[ResourceIdentifier]` to the property that uniquely identifies a resource. This signals to endpoint infrastructure (e.g. `Dosaic.Plugins.Endpoints.RestResourceEntity`) which property maps to the `{id}` route segment.

```csharp
using Dosaic.Plugins.Endpoints.Abstractions;

public class OrderModel
{
    [ResourceIdentifier]
    public Guid Id { get; set; }

    public string CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
}
```

### Reflecting on the attribute at runtime

Because the attribute is placed on properties, you can discover the identifier property of any model via standard reflection:

```csharp
using Dosaic.Plugins.Endpoints.Abstractions;
using System.Reflection;

var identifierProperty = typeof(OrderModel)
    .GetProperties()
    .FirstOrDefault(p => p.GetCustomAttribute<ResourceIdentifierAttribute>() != null);

Console.WriteLine(identifierProperty?.Name); // "Id"
```
