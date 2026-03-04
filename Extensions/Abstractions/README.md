# Dosaic.Extensions.Abstractions

Shared value-object and pagination abstractions for Dosaic-based .NET services. Provides lightweight, framework-agnostic types for paginated responses, monetary values, and typed quantity values.

## Installation

```shell
dotnet add package Dosaic.Extensions.Abstractions
```

## Types

| Type | Kind | Description |
|---|---|---|
| `Page` | record | Pagination metadata: current page index, page size, total elements, total pages |
| `PagedList<T>` | record | Combines a list of items with a `Page` metadata object |
| `CurrencyValue` | class | Monetary value with a decimal amount, currency symbol, and ISO currency code |
| `Quantity<T>` | abstract record | Generic quantity with a typed `Value` and a `Unit` string |
| `QuantityCount` | abstract record | Specialisation of `Quantity<int>` with the unit fixed to `"Count"` |

## Usage

### Page

`Page` holds pagination metadata returned alongside a result set. Null `size` defaults to `int.MaxValue` (unbounded), and null `current` defaults to `0`.

```csharp
// Create manually
var page = new Page(size: 10, current: 2, totalElements: 100, totalPages: 10);

Console.WriteLine(page.Current);       // 2
Console.WriteLine(page.Size);          // 10
Console.WriteLine(page.TotalElements); // 100
Console.WriteLine(page.TotalPages);    // 10

// Null-safe defaults
var unbounded = new Page(size: null, current: null, totalElements: 50, totalPages: 1);
Console.WriteLine(unbounded.Size);    // 2147483647 (int.MaxValue)
Console.WriteLine(unbounded.Current); // 0
```

---

### PagedList\<T\>

`PagedList<T>` combines a page of items with auto-calculated `Page` metadata. Total pages are computed as `⌈totalElements / size⌉`.

```csharp
var items = new List<string> { "alpha", "beta", "gamma" };

var result = new PagedList<string>(
    items: items,
    totalElements: 10,
    page: 2,
    size: 3);

// Access items
foreach (var item in result.Items)
    Console.WriteLine(item);

// Access pagination metadata
Console.WriteLine(result.Page.Current);       // 2
Console.WriteLine(result.Page.Size);          // 3
Console.WriteLine(result.Page.TotalElements); // 10
Console.WriteLine(result.Page.TotalPages);    // 4  (⌈10/3⌉)
```

A typical use in a query handler or repository:

```csharp
public PagedList<OrderDto> GetOrders(int page, int size)
{
    var query = _dbContext.Orders.AsQueryable();
    var total = query.Count();
    var orders = query.Skip(page * size).Take(size).ToList();
    return new PagedList<OrderDto>(orders.Select(MapToDto), total, page, size);
}
```

---

### CurrencyValue

`CurrencyValue` represents a monetary amount together with its currency. Built-in factory methods cover Euro and US Dollar; custom currencies can be constructed directly.

```csharp
// Built-in factories
var price   = CurrencyValue.Euro(19.99m);
var payment = CurrencyValue.Dollar(9.99m);

Console.WriteLine(price.Value);          // 19.99
Console.WriteLine(price.CurrencySymbol); // €
Console.WriteLine(price.CurrencyCode);   // EUR

// Custom currency
var pounds = new CurrencyValue(999.99m, "£", "GBP");

// Serialises cleanly with System.Text.Json
var json = JsonSerializer.Serialize(price, new JsonSerializerOptions(JsonSerializerDefaults.Web));
// {"value":19.99,"currencySymbol":"\u20AC","currencyCode":"EUR"}

var restored = JsonSerializer.Deserialize<CurrencyValue>(json,
    new JsonSerializerOptions(JsonSerializerDefaults.Web));
```

---

### Quantity\<T\>

`Quantity<T>` is the base for any domain-specific typed quantity. Derive a concrete record to attach semantic meaning to a value/unit pair.

```csharp
// Define a custom quantity type
public record Celsius(decimal Value) : Quantity<decimal>(Value, "°C");
public record Kilograms(decimal Value) : Quantity<decimal>(Value, "kg");

var temp   = new Celsius(36.6m);
var weight = new Kilograms(75m);

Console.WriteLine($"{temp.Value} {temp.Unit}");   // 36.6 °C
Console.WriteLine($"{weight.Value} {weight.Unit}"); // 75 kg

// Record equality is value-based
var a = new Celsius(36.6m);
var b = new Celsius(36.6m);
Console.WriteLine(a == b); // True

// Different types are never equal even with the same value
var c = new Kilograms(36.6m); // not equal to a Celsius
```

---

### QuantityCount

`QuantityCount` fixes the unit to `"Count"` and the value type to `int`. Derive it for domain-specific integer counters.

```csharp
// Define domain-specific count types
public record LoginCount(int Value)   : QuantityCount(Value);
public record FailedAttempts(int Value) : QuantityCount(Value);

var logins   = new LoginCount(42);
var failures = new FailedAttempts(3);

Console.WriteLine(logins.Value);   // 42
Console.WriteLine(logins.Unit);    // Count
Console.WriteLine(failures.Value); // 3
Console.WriteLine(failures.Unit);  // Count

// Each derived type maintains its own identity
Console.WriteLine(logins == failures); // False (different types)

// Serialises with System.Text.Json
var json = JsonSerializer.Serialize(logins, new JsonSerializerOptions(JsonSerializerDefaults.Web));
// {"value":42,"unit":"Count"}
```



