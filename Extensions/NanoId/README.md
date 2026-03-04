# Dosaic.Extensions.NanoIds

`Dosaic.Extensions.NanoIds` provides a strongly-typed `NanoId` value type for use across Dosaic services. It wraps [NanoidDotNet](https://github.com/codeyu/nanoid-net) to generate compact, URL-safe, unique identifiers and adds first-class support for JSON and YAML serialization, entity decoration via attributes, collision-safe length presets, and implicit conversion to/from `string`.

## Installation

```shell
dotnet add package Dosaic.Extensions.NanoIds
```

Or add a package reference directly to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Extensions.NanoIds" Version="" />
```

## Features

- **Strongly-typed ID** — `NanoId` is a distinct type, not a plain `string`, preventing accidental ID mix-ups across entity types.
- **No-look-alike alphabet** — uses an alphabet that excludes visually ambiguous characters (e.g. `0`, `O`, `I`, `l`) for safer human-readable IDs.
- **Attribute-driven generation** — decorate any class with `[NanoId(length)]` or `[NanoId(length, "prefix_")]` and generate correctly-sized, prefixed IDs with a single call.
- **Pre-calculated safe lengths** — `NanoIdConfig.Lengths` documents the number of IDs needed to reach a 1 % collision probability for each ID length (L2–L24).
- **Full serialization support** — built-in `JsonConverter` and `IYamlConverter` so `NanoId` serializes as a plain string in both JSON and YAML.
- **Implicit `string` conversions** — assign a `string` literal to a `NanoId` variable and vice versa without an explicit cast.
- **Value semantics** — implements `IComparable`, `IComparable<NanoId>`, and `IEquatable<NanoId>` with `==` / `!=` operator support.
- **`INanoId` interface** — a lightweight marker interface for entity classes that expose a `NanoId Id` property.

## Core Types

### `NanoId`

The central value type. Wraps a `string` value and exposes factory methods for generating new IDs.

```csharp
// Construct from a known string (e.g. loaded from a database)
var id = new NanoId("abc123");

// Implicit conversion from string
NanoId id2 = "abc123";

// Implicit conversion to string
string raw = id2;

// Parse (returns null for null input)
var id3 = NanoId.Parse(someNullableString);
```

### `INanoId`

Implement this interface on any entity class that owns a `NanoId` primary key:

```csharp
public class Order : INanoId
{
    public NanoId Id { get; set; }
    public string Description { get; set; }
}
```

### `NanoIdAttribute`

Decorate entity classes with `[NanoId]` to configure the length (and optional prefix) of IDs generated for that type.

| Parameter | Type | Description |
|---|---|---|
| `length` | `byte` | Number of random characters to generate |
| `prefix` | `string` | Optional string prepended to every generated ID (default: `""`) |

```csharp
// 12-character ID, no prefix  →  e.g. "wK3mRpNtVx8q"
[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L12)]
public class Product : INanoId
{
    public NanoId Id { get; set; }
}

// 8 random characters with "ord_" prefix  →  e.g. "ord_Kp4mRx8t"
[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L8, "ord_")]
public class Order : INanoId
{
    public NanoId Id { get; set; }
}
```

The `LengthWithPrefix` property gives the total stored length:

```csharp
var attr = typeof(Order).GetCustomAttribute<NanoIdAttribute>();
// attr.Prefix           == "ord_"
// attr.Length           == 8
// attr.LengthWithPrefix == 12  (8 random + 4 prefix characters)
```

### `NanoIdConfig`

Static configuration used internally during ID generation.

```csharp
// The alphabet used for all generated IDs
// (no-look-alike digits + no-look-alike letters)
string alphabet = NanoIdConfig.Alphabet;
```

#### `NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters`

Pre-calculated constants that map an ID length to the number of IDs required to reach a 1 % collision probability. Use these constants instead of magic numbers.

| Constant | Length | IDs before ~1% collision |
|---|---|---|
| `L2` | 2 | 6 |
| `L3` | 3 | 48 |
| `L4` | 4 | 340 |
| `L5` | 5 | 2 K |
| `L6` | 6 | 16 K |
| `L7` | 7 | 116 K |
| `L8` | 8 | 817 K |
| `L9` | 9 | 5 M |
| `L10` | 10 | 40 M |
| `L11` | 11 | 280 M |
| `L12` | 12 | 1 B |
| `L13` | 13 | 13 B |
| `L14` | 14 | 96 B |
| `L15` | 15 | 673 B |
| `L16` | 16 | 4 T |
| `L17` | 17 | 32 T |
| `L18` | 18 | 230 T |
| `L19` | 19 | 1 616 T |
| `L20` | 20 | 11 312 T |
| `L24` | 24 | 27 161 781 T |

> Figures are based on [zelark.github.io/nano-id-cc](https://zelark.github.io/nano-id-cc/).

## Usage

### Defining entity types

```csharp
using Dosaic.Extensions.NanoIds;

[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L12)]
public class BlogPost : INanoId
{
    public NanoId Id { get; set; }
    public string Title { get; set; }
}

[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L8, "usr_")]
public class User : INanoId
{
    public NanoId Id { get; set; }
    public string Email { get; set; }
}
```

### Generating new IDs

Use the generic overload when the type is known at compile time:

```csharp
// Generic — type resolved at compile time
NanoId postId = NanoId.NewId<BlogPost>(); // e.g. "wK3mRpNtVx8q"
NanoId userId = NanoId.NewId<User>();     // e.g. "usr_Kp4mRx8t"
```

Use the `Type` overload when the type is only known at runtime (e.g. dynamic seeding scripts):

```csharp
Type entityType = typeof(BlogPost);
NanoId id = NanoId.NewId(entityType);
```

> `NewId` throws `ArgumentException` if the target type does not have a `[NanoId]` attribute.

### Generating IDs for database seed data

A common pattern is to pre-generate a fixed set of readable IDs for seed data files. The test project ships an `[Explicit]` helper test for exactly this purpose:

```csharp
[Test]
[Explicit]
public void GenerateStaticIdsForDatabaseSeedData()
{
    var modelType = typeof(BlogPost);
    for (var i = 0; i < 10; i++)
    {
        TestContext.Out.WriteLine($"{modelType.Name}: {NanoId.NewId(modelType)}");
    }
}
```

Run it once with your preferred type, then copy the output into your seed-data files.

### Value semantics

`NanoId` supports all standard equality and comparison operations:

```csharp
var a = new NanoId("abc");
var b = new NanoId("abc");
var c = new NanoId("xyz");

bool eq   = a == b;          // true
bool neq  = a != c;          // true
bool same = a.Equals(b);     // true
int  cmp  = a.CompareTo(c);  // negative  (a < c lexicographically)
```

Passing a non-`NanoId` object to `CompareTo(object)` throws `ArgumentException`.
Constructing a `NanoId` with a `null` string throws `ArgumentNullException`.

### Implicit string conversions

```csharp
// string  →  NanoId
NanoId id = "abc123";

// NanoId  →  string
string raw = id;

// Works transparently in interpolated strings
Console.WriteLine($"Processing {id}"); // prints "abc123"
```

### Serialization

`NanoId` serializes as a plain JSON/YAML string out of the box. No extra configuration is required when using Dosaic's default serialization stack.

**JSON**

```csharp
public class OrderDto
{
    public NanoId Id { get; init; }
}

var dto = new OrderDto { Id = NanoId.Parse("ord_Kp4mRx8t") };
string json = dto.Serialize();
// → {"id":"ord_Kp4mRx8t"}

OrderDto restored = json.Deserialize<OrderDto>();
// restored.Id.Value == "ord_Kp4mRx8t"
```

**YAML**

```csharp
string yaml = dto.Serialize(SerializationMethod.Yaml);
// → id: ord_Kp4mRx8t

OrderDto restoredYaml = yaml.Deserialize<OrderDto>(SerializationMethod.Yaml);
// restoredYaml.Id.Value == "ord_Kp4mRx8t"
```

### `ToString` and span formatting

```csharp
var id = new NanoId("abc123");

// Default → returns the raw value; suitable for logging and interpolation
id.ToString();                              // "abc123"

// With a format provider → "Value: <raw>"
id.ToString(CultureInfo.InvariantCulture);  // "Value: abc123"

// Span-based formatting
Span<char> buf = stackalloc char[64];
id.TryFormat(buf, out int written, default, null);
// new string(buf[..written]) == "Value: abc123"
```

## No plugin configuration required

`Dosaic.Extensions.NanoIds` is a pure library — it does not register any DI services or middleware and requires no entries in `appsettings.yml`. Simply reference the package and use the types directly.
