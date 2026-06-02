# Dosaic.Extensions.Localization.Abstractions

`Dosaic.Extensions.Localization.Abstractions` is the annotation package for the Dosaic localization system. It provides the `[LocalizedName]` attribute and the `Locale` enum that the companion source generator (`Dosaic.Extensions.Localization.Generator`) reads at compile time to produce a static `EntityLabels` lookup class.

## Installation

```shell
dotnet add package Dosaic.Extensions.Localization.Abstractions
```

Or as a `PackageReference` in your `.csproj`:

```xml
<PackageReference Include="Dosaic.Extensions.Localization.Abstractions" Version="" />
```

To also generate the `EntityLabels` lookup class, add the generator package:

```xml
<PackageReference Include="Dosaic.Extensions.Localization.Generator" Version=""
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## API

### `[LocalizedName]`

Annotate classes, enums, enum members, properties, and fields with translated display names.

```csharp
using Dosaic.Extensions.Localization;

[LocalizedName(en: "Order", de: "Bestellung")]
public class Order
{
    [LocalizedName(en: "Order Number", de: "Bestellnummer")]
    public string OrderNumber { get; set; }
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

| Property | Type | Description |
|---|---|---|
| `En` | `string` | English display name (defaults to `""`) |
| `De` | `string` | German display name (defaults to `""`) |

Valid targets: `Class`, `Enum`, `Property`, `Field` (including enum members).

### `Locale`

Strongly-typed culture discriminator used as a parameter in the generated `EntityLabels.Get(...)` overloads.

```csharp
public enum Locale
{
    En,
    De
}
```

Use `Locale.En` / `Locale.De` instead of culture strings when calling the generated API.

## Related packages

| Package | Purpose |
|---|---|
| `Dosaic.Extensions.Localization.Abstractions` | This package — attributes and `Locale` enum |
| `Dosaic.Extensions.Localization.Generator` | Roslyn source generator — emits `EntityLabels.g.cs` and optional JSON translation files at compile time |
