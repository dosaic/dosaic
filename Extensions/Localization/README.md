# Dosaic.Extensions.Localization.Abstractions

`Dosaic.Extensions.Localization.Abstractions` provides the `LocalizedNameAttribute` and `Locale` enum used to annotate types and their members with human-readable labels in multiple languages. It is designed to be used together with [`Dosaic.Extensions.Localization.Generator`](Generator/README.md), which reads these annotations at compile time and generates a type-safe lookup class.

## Installation

Add both packages to your project:

```shell
dotnet add package Dosaic.Extensions.Localization.Abstractions
dotnet add package Dosaic.Extensions.Localization.Generator
```

Or as `PackageReference` entries in your `.csproj`:

```xml
<!-- Attributes — normal compile-time reference -->
<PackageReference Include="Dosaic.Extensions.Localization.Abstractions" Version="" />

<!-- Generator — runs as a Roslyn source generator, not a runtime dependency -->
<PackageReference Include="Dosaic.Extensions.Localization.Generator" Version=""
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Features

- **`[LocalizedName]`** — decorate classes, enums, enum members, properties, and fields with `en` and `de` labels
- **`Locale` enum** — type-safe culture values (`Locale.En`, `Locale.De`) replace magic strings in all API calls
- **Compile-time resolution** — labels are embedded into the generated lookup class; no reflection at runtime
- **Type-safe lookup** — expression-based and generic overloads resolve labels without magic strings
- **Configurable default culture** — set `EntityLabels.DefaultCulture` once at startup
- **Fallback** — returns the member or type name when no translation exists for the requested culture
- **JSON export** — optionally write translation files per culture during build (see [Generator](Generator/README.md))

## Usage

### Annotating types and members

`[LocalizedName]` can be placed on classes, enums, enum members, properties, and fields:

```csharp
using Dosaic.Extensions.Localization;

[LocalizedName(en: "Order", de: "Bestellung")]
public class Order
{
    [LocalizedName(en: "Order Number", de: "Bestellnummer")]
    public string OrderNumber { get; set; }

    [LocalizedName(en: "Customer", de: "Kunde")]
    public string CustomerName { get; set; }
}

[LocalizedName(en: "Order Status", de: "Bestellstatus")]
public enum OrderStatus
{
    [LocalizedName(en: "Active", de: "Aktiv")]
    Active,

    [LocalizedName(en: "Cancelled", de: "Storniert")]
    Cancelled
}
```

### Resolving labels

After adding the generator the `EntityLabels` class is available in the `Dosaic.Extensions.Localization` namespace:

```csharp
using Dosaic.Extensions.Localization;

// Type label — class or enum
EntityLabels.Get<Order>();                        // "Bestellung" (DefaultCulture)
EntityLabels.Get<OrderStatus>(Locale.En);         // "Order Status"

// Property / field — expression-based
EntityLabels.Get<Order>(x => x.OrderNumber);             // "Bestellnummer"
EntityLabels.Get<Order>(x => x.OrderNumber, Locale.En);  // "Order Number"

// Enum member — pass the value directly
EntityLabels.Get(OrderStatus.Active);             // "Aktiv"
EntityLabels.Get(OrderStatus.Active, Locale.En);  // "Active"

// Raw key — when type is not known at compile time
EntityLabels.Get("Order");                        // "Bestellung"
EntityLabels.Get("Order.OrderNumber");            // "Bestellnummer"
EntityLabels.Get("Order.OrderNumber", Locale.En); // "Order Number"
```

### Changing the default culture

```csharp
// Set once at application startup
EntityLabels.DefaultCulture = Locale.De;
```

**Default:** `Locale.En`
