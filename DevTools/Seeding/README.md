# Dosaic.DevTools.Seeding

A developer tool for seeding Entity Framework Core databases with realistic fake data. Built on top of [Bogus](https://github.com/bchavez/Bogus), it introspects your `DbContext` model, respects foreign key relationships, and fills tables in the correct dependency order — with full control over row counts and relationship cardinality.

Intended for use in local development environments, integration tests, and demo setups.

---

## Installation

```bash
dotnet add package Dosaic.DevTools.Seeding
```

---

## Features

- **Automatic entity discovery** — reads all non-owned entity types from the EF Core model
- **Topological sort** — seeds principal tables before dependent tables, honoring every foreign key
- **FK-aware generation** — pulls existing principal keys from the database and assigns them to dependent rows
- **Relation count control** — configure exactly how many dependents to create per principal (exact, range, or pick-list)
- **Transitive FK sync** — when an entity has two foreign keys that share a common ancestor, the redundant FK column is automatically kept consistent (e.g. `OrderLine.CustomerId` is synced to match `Order.CustomerId`)
- **Per-type and global row counts** — set a default count for all types and override individually
- **Ignore list** — skip specific entity types entirely
- **Batched saves** — configurable `SaveChanges` batch size to control memory pressure
- **Extensible fake data rules** — implement `IFakeDataSetup<T>` to customize generated values per entity type; implementations are auto-discovered via assembly scanning
- **Custom type rules** — register global `Faker` rules for primitive types (e.g. always generate valid `Guid` or a currency-formatted `decimal`)

---

## Core Types

| Type | Description |
|---|---|
| `EfFakeDataSeeder` | Executes the seeding operation against a `DbContext` |
| `EfFakeDataSeederConfig` | Fluent configuration builder for the seeder |
| `FakeData` | Wrapper around Bogus `Faker<T>` with auto-discovery of `IFakeDataSetup<T>` implementations |
| `FakeDataConfig` | Configuration for `FakeData` (locale, strict mode, global type rules) |
| `IFakeDataSetup<T>` | Interface for declaring per-entity Bogus rules |

---

## Usage

### Basic seeding

Seed all entity types in the model with the default count (10 rows each):

```csharp
await using var context = new MyDbContext();

var config = EfFakeDataSeederConfig.For(context);
var seeder = new EfFakeDataSeeder(config);

await seeder.SeedAsync(CancellationToken.None);
```

### Controlling row counts

```csharp
var config = EfFakeDataSeederConfig.For(context)
    .WithTotalCount<Customer>(50)       // seed exactly 50 customers
    .WithTotalCount<Product>(20)        // seed exactly 20 products
    .WithDefaultCountPerEntityType(5);  // all other entity types: 5 rows each
```

### Controlling relationship cardinality

Use `WithRelationCount` to declare how many dependent rows to create per principal:

```csharp
// Each customer gets exactly 2 orders
var config = EfFakeDataSeederConfig.For(context)
    .WithTotalCount<Customer>(5)
    .WithRelationCount<Order>(x => x.Customer, 2);
```

```csharp
// Each order gets between 1 and 10 order lines
var config = EfFakeDataSeederConfig.For(context)
    .WithRelationCount<OrderLine>(x => x.Order, min: 1, max: 10);
```

```csharp
// Each order gets either 1, 2, or 3 order lines (picked at random)
var config = EfFakeDataSeederConfig.For(context)
    .WithRelationCount<Order>(x => x.Customer, 1, 2, 3);
```

### Ignoring entity types

```csharp
var config = EfFakeDataSeederConfig.For(context)
    .WithIgnore<AuditLog>()
    .WithIgnore<MigrationHistory>();
```

### Configuring the batch size

By default `SaveChangesAsync` is called every 200 rows. Adjust to tune memory vs. I/O:

```csharp
var config = EfFakeDataSeederConfig.For(context)
    .WithBatchSize(500);
```

### Custom fake data rules with `IFakeDataSetup<T>`

Implement `IFakeDataSetup<T>` to attach Bogus rules to a specific entity type. Implementations are discovered automatically from all loaded assemblies — no registration required.

```csharp
using Bogus;
using Dosaic.Testing.NUnit.Extensions;

public class CustomerFakeDataSetup : IFakeDataSetup<Customer>
{
    public void ConfigureRules(Faker<Customer> faker)
    {
        faker.RuleFor(x => x.Id,    f => f.Random.Guid());
        faker.RuleFor(x => x.Name,  f => f.Name.FullName());
        faker.RuleFor(x => x.Email, f => f.Internet.Email());
        faker.RuleFor(x => x.State, f => f.PickRandom<CustomerState>());
    }
}

public class ProductFakeDataSetup : IFakeDataSetup<Product>
{
    public void ConfigureRules(Faker<Product> faker)
    {
        faker.RuleFor(x => x.Id,    f => f.Random.Guid());
        faker.RuleFor(x => x.Name,  f => f.Commerce.Product());
        faker.RuleFor(x => x.Price, f => Math.Round(f.Random.Decimal(1m, 500m), 2));
    }
}
```

### Configuring `FakeData` globally

Use `FakeData.ConfigureInstance` once at application startup to customise locale, strict mode, or primitive type rules:

```csharp
var fakeDataConfig = new FakeDataConfig
{
    Locale = "de",
    UseStrictMode = false,
};

fakeDataConfig.AddTypeRule<Guid>(f => f.Random.Guid());
fakeDataConfig.AddTypeRule<decimal>(f => Math.Round(f.Random.Decimal(0m, 1000m), 2));

FakeData.ConfigureInstance(fakeDataConfig);
```

Pass a custom `FakeData` instance directly to the config when needed:

```csharp
var fakeData = new FakeData(fakeDataConfig);
var config = EfFakeDataSeederConfig.For(context, fakeData);
```

### Using `FakeData` standalone

`FakeData` can also be used independently to generate test objects outside of seeding:

```csharp
var fakeData = FakeData.Instance;

// Generate a single instance
var customer = fakeData.Fake<Customer>();

// Generate with inline customisation
var vipCustomer = fakeData.Fake<Customer>(c => c.State = CustomerState.Active);

// Generate a list
var products = fakeData.Fakes<Product>(10);

// Generate a list with customisation
var orders = fakeData.Fakes<Order>(5, (faker, o) =>
{
    o.Date = faker.Date.Recent().ToUniversalTime();
});

// Access the raw Bogus Faker for one-off values
string randomName = fakeData.Faker.Name.FullName();
```

---

## Full example

```csharp
// 1. Define fake data rules
public class OrderFakeDataSetup : IFakeDataSetup<Order>
{
    public void ConfigureRules(Faker<Order> faker)
    {
        faker.RuleFor(x => x.Id,   f => f.Random.Guid());
        faker.RuleFor(x => x.Date, f => f.Date.Past().ToUniversalTime());
    }
}

// 2. Configure and run the seeder
await using var context = new ShopDbContext();

var config = EfFakeDataSeederConfig.For(context)
    .WithTotalCount<Customer>(50)
    .WithTotalCount<Product>(100)
    .WithRelationCount<Order>(x => x.Customer, min: 1, max: 5)
    .WithRelationCount<OrderLine>(x => x.Order, min: 1, max: 10)
    .WithIgnore<OutboxMessage>()
    .WithBatchSize(500);

var seeder = new EfFakeDataSeeder(config);
await seeder.SeedAsync();
```

This will:
1. Seed 50 `Customer` rows, then 100 `Product` rows
2. Seed `Order` rows — 1–5 per customer (50–250 orders total)
3. Seed `OrderLine` rows — 1–10 per order, with `OrderLine.CustomerId` automatically synced to the parent order's `CustomerId`
4. Skip `OutboxMessage` entirely
5. Flush to the database every 500 rows

---

## Configuration reference

### `EfFakeDataSeederConfig`

| Method | Default | Description |
|---|---|---|
| `EfFakeDataSeederConfig.For(context)` | — | Creates a config for the given `DbContext` using `FakeData.Instance` |
| `EfFakeDataSeederConfig.For(context, fakeData)` | — | Creates a config with a custom `FakeData` instance |
| `.WithDefaultCountPerEntityType(n)` | `10` | Row count for entity types without an explicit override |
| `.WithTotalCount<T>(n)` | — | Row count for a specific entity type |
| `.WithIgnore<T>()` | — | Exclude an entity type from seeding |
| `.WithRelationCount<T>(nav, count)` | — | Exact number of dependents per principal |
| `.WithRelationCount<T>(nav, min, max)` | — | Dependents per principal drawn from `[min, max]` |
| `.WithRelationCount<T>(nav, params int[])` | — | Dependents per principal chosen from the given options |
| `.WithBatchSize(n)` | `200` | Number of entities added before each `SaveChangesAsync` call |

### `FakeDataConfig`

| Property / Method | Default | Description |
|---|---|---|
| `Locale` | `"en"` | Bogus locale string (e.g. `"de"`, `"fr"`) |
| `UseStrictMode` | `false` | Enables Bogus strict mode — every property must have a rule |
| `.AddTypeRule<T>(Func<Faker, T>)` | — | Global rule applied to all fakers for the given CLR type |
