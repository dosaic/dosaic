# Dosaic.Plugins.Mapping.Mapster

`Dosaic.Plugins.Mapping.Mapster` is a Dosaic plugin that wires [Mapster](https://github.com/MapsterMapper/Mapster) into
the Dosaic plugin pipeline. It automatically scans all application assemblies at startup, discovers types annotated
with `[MapFrom<TSource>]`, and registers the corresponding Mapster mapping rules in
`TypeAdapterConfig.GlobalSettings` — no manual mapping registration required.

## Installation

```shell
dotnet add package Dosaic.Plugins.Mapping.Mapster
```

Or add a package reference directly to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Mapping.Mapster" Version="" />
```

## Configuration

No configuration is required. `MapsterPlugin` is discovered and activated automatically by the Dosaic source
generator (`Dosaic.Hosting.Generator`). At startup it calls `IImplementationResolver.FindAssemblies()` to obtain
every assembly in the application and passes them to the internal `MapsterInitializer`, which registers all
`[MapFrom<T>]`-based rules into Mapster's global configuration.

There are no `appsettings.yml` / `appsettings.json` keys for this plugin.

## Usage

### Basic property rename

Use `[MapFrom<TSource>(nameof(TSource.Property))]` on the **destination** property to map it from a
differently-named property on the source type.

```csharp
public class DbModel
{
    public string Id { get; set; }
    public string LongName { get; set; }
}

public class ModelDto
{
    public string Id { get; set; }

    [MapFrom<DbModel>(nameof(DbModel.LongName))]
    public string Name { get; set; }
}

// Mapping
var dbModel = new DbModel { Id = "1", LongName = "Hello World" };
var dto = dbModel.Adapt<ModelDto>();
// dto.Name == "Hello World"
```

### Nested navigation path

Pass multiple property names to traverse nested objects. Each element in the array is a step in the navigation
chain evaluated against the current traversal position.

```csharp
public class Order
{
    public int Id { get; set; }
    public Address ShippingAddress { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }

    // Maps from order.ShippingAddress.City
    [MapFrom<Order>(nameof(Order.ShippingAddress), nameof(Address.City))]
    public string City { get; set; }
}

var order = new Order { Id = 42, ShippingAddress = new Address { Street = "Main St", City = "Berlin" } };
var dto = order.Adapt<OrderDto>();
// dto.City == "Berlin"
```

### Collection projection — scalar values

When the navigation path passes through a collection, each element is projected individually. The target property
type must be a compatible `IEnumerable<T>`.

```csharp
public class BlogPost
{
    public int Id { get; set; }
    public List<Tag> Tags { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class BlogPostDto
{
    public int Id { get; set; }

    // Extracts Tag.Name from every element in BlogPost.Tags
    [MapFrom<BlogPost>(nameof(BlogPost.Tags), nameof(Tag.Name))]
    public IEnumerable<string> TagNames { get; set; }
}

var post = new BlogPost
{
    Id = 1,
    Tags = [new Tag { Id = 10, Name = "dotnet" }, new Tag { Id = 11, Name = "mapster" }]
};
var dto = post.Adapt<BlogPostDto>();
// dto.TagNames == ["dotnet", "mapster"]
```

### Collection projection — object mapping

When the navigation ends at a collection of objects, each element is adapted to the target element type using the
globally registered Mapster rules.

```csharp
public class SourceModel
{
    public List<NestedSource> Items { get; set; }
}

public class NestedSource
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class NestedTarget
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class TargetModel
{
    // Each NestedSource element is adapted to NestedTarget
    [MapFrom<SourceModel>(nameof(SourceModel.Items))]
    public IEnumerable<NestedTarget> Items { get; set; }
}

var source = new SourceModel
{
    Items = [new NestedSource { Id = 1, Name = "A" }, new NestedSource { Id = 2, Name = "B" }]
};
var target = source.Adapt<TargetModel>();
// target.Items contains two NestedTarget instances
```

### Null-safe collection mapping

If the source collection is `null`, the mapped destination property is also `null` — no `NullReferenceException`
is thrown.

```csharp
public class SourceModel
{
    public List<NestedSource> Items { get; set; } // null
}

public class TargetModel
{
    [MapFrom<SourceModel>(nameof(SourceModel.Items))]
    public IEnumerable<NestedTarget> Items { get; set; }
}

var source = new SourceModel { Items = null };
var target = source.Adapt<TargetModel>();
// target.Items == null  (no exception)
```

### Multiple `[MapFrom]` attributes on a single property

The attribute allows `AllowMultiple = true`, so a single destination property can have multiple source mappings
applied (e.g., for different source types).

```csharp
public class TargetModel
{
    [MapFrom<SourceA>(nameof(SourceA.Title))]
    [MapFrom<SourceB>(nameof(SourceB.Headline))]
    public string Name { get; set; }
}
```

### EF Core LINQ projection (`ProjectToType<T>`)

Because the mappings are registered as Mapster expression rules, they are fully translatable to SQL when used with
EF Core's `ProjectToType<T>()` extension.

```csharp
// Retrieve only the columns you need — translated to SQL
var dtos = await dbContext.Orders
    .Where(o => o.CustomerId == customerId)
    .ProjectToType<OrderDto>()
    .ToListAsync();
```

## Features

- **Zero configuration** — no YAML/JSON settings required; the plugin activates automatically via the Dosaic
  source generator.
- **Attribute-driven mapping** — declare all mapping rules inline on destination types using
  `[MapFrom<TSource>(navigationPath)]`, keeping mapping logic co-located with the DTO.
- **Deep navigation paths** — navigate arbitrarily deep object graphs by providing a sequence of property names;
  each name is resolved against the current traversal type.
- **Collection projection to scalars** — extract a scalar property from every element of a source collection into
  an `IEnumerable<TScalar>`.
- **Collection projection to objects** — map a source collection to a destination collection of a different
  element type, with each element adapted via Mapster.
- **Null-safe collections** — `null` source collections produce `null` destination properties without throwing.
- **EF Core LINQ projection** — registered expression rules are LINQ-translatable, enabling efficient
  `ProjectToType<T>()` queries that are converted to SQL.
- **Global rule registration** — rules land in `TypeAdapterConfig.GlobalSettings`, making them available
  everywhere `Adapt<T>()` or `ProjectToType<T>()` is called with no additional setup.
- **Assembly scanning** — leverages `IImplementationResolver.FindAssemblies()` so every assembly in the
  application is scanned, including plugin assemblies.


