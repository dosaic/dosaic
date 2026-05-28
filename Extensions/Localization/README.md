# Dosaic.Extensions.Localization

`Dosaic.Extensions.Localization` provides the `LocalizedNameAttribute` used to annotate model properties with human-readable labels in multiple languages. It is designed to be used together with [`Dosaic.Extensions.Localization.Generator`](Generator/README.md), which reads these annotations at compile time and generates a type-safe lookup class.

## Installation

Add both packages to your project:

```shell
dotnet add package Dosaic.Extensions.Localization
dotnet add package Dosaic.Extensions.Localization.Generator
```

Or as `PackageReference` entries in your `.csproj`:

```xml
<!-- Attributes — normal compile-time reference -->
<PackageReference Include="Dosaic.Extensions.Localization" Version="" />

<!-- Generator — runs as a Roslyn source generator, not a runtime dependency -->
<PackageReference Include="Dosaic.Extensions.Localization.Generator" Version=""
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Features

- **`[LocalizedName]`** — decorate any property with `en` and `de` labels
- **Compile-time resolution** — labels are embedded into the generated lookup class; no reflection at runtime
- **Type-safe lookup** — `EntityPropertyLabels.Get<T>(x => x.Property)` resolves the label without magic strings
- **Configurable default culture** — set `EntityPropertyLabels.DefaultCulture` once at startup
- **JSON export** — optionally write translation files per culture during build (see [Generator](Generator/README.md))

## Usage

### Annotating properties

```csharp
using Dosaic.Extensions.Localization;

public class Order
{
    [LocalizedName(en: "Order Number", de: "Bestellnummer")]
    public string OrderNumber { get; set; }

    [LocalizedName(en: "Customer", de: "Kunde")]
    public string CustomerName { get; set; }
}
```

### Resolving labels

After adding the generator the `EntityPropertyLabels` class is available in the `Dosaic.Extensions.Localization` namespace:

```csharp
using Dosaic.Extensions.Localization;

// Type-safe — expression-based, no magic strings
string label = EntityPropertyLabels.Get<Order>(x => x.OrderNumber);        // "Bestellnummer" (default culture)
string label = EntityPropertyLabels.Get<Order>(x => x.OrderNumber, "en");  // "Order Number"

// String-based — when type is not known at compile time
string label = EntityPropertyLabels.Get("Order", "OrderNumber");            // "Bestellnummer"
string label = EntityPropertyLabels.Get("Order", "OrderNumber", "en");      // "Order Number"
```

If no translation exists for the requested culture the property name is returned as fallback.

### Changing the default culture

```csharp
// Set once at application startup
EntityPropertyLabels.DefaultCulture = "en";

EntityPropertyLabels.Get<Order>(x => x.CustomerName); // "Customer"
```

**Default:** `"en"`
