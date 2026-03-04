# Dosaic.Plugins.Persistence.EfCore.Abstractions

`Dosaic.Plugins.Persistence.EfCore.Abstractions` is a Dosaic plugin that provides the foundational Entity Framework Core building blocks for all EF Core–based database access in the Dosaic ecosystem. It delivers a strongly-typed `IDb` context abstraction, NanoId-based primary keys, a before/after trigger pipeline, automatic audit tracking with full change history, event sourcing primitives, business-logic interception, and a set of `ModelBuilder` extensions for consistent schema conventions.

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.EfCore.Abstractions
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.EfCore.Abstractions" Version="" />
```

## Features

- **`IDb` abstraction** — a thin, testable interface wrapping an EF Core `DbContext`
- **`EfCoreDbContext`** — abstract base context that wires up the trigger/interceptor pipeline automatically on every `SaveChangesAsync`
- **NanoId primary keys** — `IModel` / `Model` base types backed by `NanoId`; `DbNanoIdPrimaryKeyAttribute` configures length and optional prefix
- **`IModel` & `Model`** — common base for all persisted entities
- **Auditing** — `IAuditableModel` / `AuditableModel` add `CreatedBy`, `CreatedUtc`, `ModifiedBy`, `ModifiedUtc` automatically
- **Full change history** — `IHistory` marker + auto-generated `History<T>` tables via `HistoryTrigger<T>` and `ApplyHistories()`
- **Before/After trigger pipeline** — `IBeforeTrigger<T>` / `IAfterTrigger<T>` with ordering via `[TriggerOrder]`
- **Business logic interception** — `IBusinessLogic<TModel>` hooks into create/update/delete lifecycle
- **Event sourcing** — `AggregateEvent`, `AggregateEvent<TEnum>`, `IEventProcessor<T>`, `IEventMapper`, `IEventProjector<T>`, and `AggregatePatch`
- **`ModelBuilder` conventions** — `ApplyKeys()`, `ApplySnakeCaseNamingConventions()`, `ApplyEnumFields()`, `ApplyHistories()`, `ApplyEventSourcing()`, `ApplyAuditFields()`
- **Queryable helpers** — `ProcessAsync`, `ProcessAndGetAsync`, `UpdateGraphAsync`
- **Automatic health checks** — every registered `DbContext` is registered as a readiness health check
- **Background migrator** — `DbMigratorService<T>` retries EF Core migrations until the database is reachable
- **OpenTelemetry tracing** — EF Core instrumentation wired up through `IEfCoreConfigurator`

## Types

### Models

| Type | Description |
|---|---|
| `IModel` | Marker interface for all entities; extends `INanoId` (requires `NanoId Id`) |
| `Model` | Abstract base class implementing `IModel` with `required NanoId Id` |
| `ModelChange<T>` | Captures a single entity change with `State`, `Entity`, `PreviousEntity`, and `GetChanges()` |
| `ModelChange` | Non-generic variant used internally; convertible to `ModelChange<T>` via `ToTyped()` |

### Audit

| Type | Description |
|---|---|
| `IAuditableModel` | Extends `IModel`; adds `CreatedBy`, `CreatedUtc`, `ModifiedBy`, `ModifiedUtc` |
| `AuditableModel` | Abstract base class implementing `IAuditableModel` |
| `IHistory` | Marker interface — implement on an entity to enable automatic history tracking |
| `History` / `History<TModel>` | Auto-generated history record entity stored in a `{table}_history` table |
| `HistoryTrigger<T>` | `IAfterTrigger<T>` that writes `History<T>` entries after each save |
| `ChangeState` | Enum: `None`, `Added`, `Modified`, `Deleted` |
| `ChangeSet` / `ChangeSet<T>` | Typed list of `ModelChange` / `ModelChange<T>` entries |
| `ObjectChanges` | Dictionary of `propertyName → OldNewValue` representing a diff |
| `ExcludeFromHistoryAttribute` | Property attribute — excludes a field from history diffs |
| `ChangeTrackerExtensions` | `GetChangeSet()` / `UpdateChangeSet()` extension methods on `ChangeTracker` |
| `IUserIdProvider` | Service interface providing `UserId`, `FallbackUserId`, and `IsUserInteraction` for audit fields |

### Database

| Type | Description |
|---|---|
| `IDb` | Core database interface: `Get<T>()`, `GetQuery<T>()`, `SaveChangesAsync()`, `BeginTransactionAsync()` |
| `EfCoreDbContext` | Abstract base `DbContext` implementing `IDb`; orchestrates the trigger/interceptor pipeline |
| `DbModel` | Static reflection helper — `GetProperties<T>()`, `GetModels(dbContextType)`, `GetNestedProperties<T>()` |
| `DbExtensions` | Extension methods: `GetEvents<TAggregate>()`, `UpdateGraphAsync<T>()` |
| `ModelExtensions` | `PatchModel<T>(values, PatchMode)` — shallow/deep-patches one entity from another |
| `QueryableExtensions` | `ProcessAsync()` and `ProcessAndGetAsync()` for async streaming queries |
| `DbMigratorService<T>` | `BackgroundService` that retries `Database.MigrateAsync()` until successful |
| `DbEnumAttribute` | Marks an `enum` type with a DB name/schema for use with `ApplyEnumFields()` |

### Identifiers

| Type | Description |
|---|---|
| `NanoIdConverter` | EF Core `ValueConverter<NanoId, string>` — registered automatically by `EfCoreDbContext` |
| `DbNanoIdPrimaryKeyAttribute` | Configures the NanoId primary key byte-length and optional string prefix |

### Triggers

| Type | Description |
|---|---|
| `IBeforeTrigger<T>` | Invoked before `SaveChangesAsync` with the typed `ITriggerContext<T>` |
| `IAfterTrigger<T>` | Invoked after `SaveChangesAsync` with the typed `ITriggerContext<T>` |
| `ITriggerContext<T>` | Exposes `ChangeSet<T>` (pending changes) and `IDb Database` |
| `TriggerOrderAttribute` | `[TriggerOrder(Order = n)]` — controls relative execution order of triggers |
| `BusinessLogicTrigger<T>` | Internal trigger that dispatches to all registered `IBusinessLogic<T>` implementations |

### Interceptors

| Type | Description |
|---|---|
| `IBusinessLogic<TModel>` | Implement to hook into `BeforeCreateAsync`, `BeforeUpdateAsync`, `BeforeDeleteAsync`, `AfterCreateAsync`, etc. |
| `IBusinessLogicInterceptor` | Low-level interceptor invoked by the trigger pipeline |

### Transactions

| Type | Description |
|---|---|
| `ITransaction` | `CommitAsync()` / `RollbackAsync()` wrapping an EF Core transaction |
| `EntityTransaction` | Default `ITransaction` implementation around `IDbContextTransaction` |

### Event Sourcing

| Type | Description |
|---|---|
| `AggregateEvent` | Base entity for events: `EventData` (JSON), `ValidFrom`, `IsDeleted`, `ModifiedBy`, `ModifiedUtc` |
| `AggregateEvent<TEnum>` | Adds typed `EventType` enum property |
| `AggregateRootAttribute<T>` | Class attribute marking an entity as the aggregate root for a given event type |
| `AggregateChildAttribute<T>` | Class attribute linking a child entity to its aggregate root via a navigation property |
| `EventMatcherAttribute` | Property attribute — marks the properties used to filter events in `GetEvents<T>()` |
| `IEventProcessor<TAggregate>` | Implement to react to a batch of `TAggregate` events |
| `IEventMapper` | Static dictionary mapping event type enum values to handler types |
| `IEventProjector<T>` | Projects an ordered sequence of mapped events into a final state |
| `AggregatePatch` | Value record for `(AggregateId, Path, Operation, Data, EntityId, EntityType)` |
| `PatchOperation` | Enum: `Add`, `Update`, `Delete` |

### Plugin & Configuration

| Type | Description |
|---|---|
| `EfCorePlugin` | Dosaic plugin entry point; registers triggers, event processors, and business logic automatically |
| `IEfCoreConfigurator` | `IPluginConfigurator` for customising EF Core OpenTelemetry tracing |

### `ModelBuilderExtensions`

| Method | Description |
|---|---|
| `ApplyKeys()` | Configures NanoId primary keys and foreign key lengths for all entities |
| `ApplyEnumFields()` | Maps enum columns to their DB type name using `[DbEnum]` |
| `ApplyHistories(modifiedByFkModel)` | Creates `History<T>` tables for all `IHistory` entities |
| `ApplyEventSourcing(modifiedByFkModel)` | Configures JSONB event data columns for all `AggregateEvent` entities |
| `ApplyAuditFields(createdByFkModel, modifiedByFkModel)` | Wires up `CreatedBy`/`CreatedUtc` defaults for `IAuditableModel` entities |
| `ApplySnakeCaseNamingConventions()` | Renames all tables, columns, keys and indexes to `snake_case` |

### `ServiceCollectionExtensions`

| Method | Description |
|---|---|
| `AddEfCoreContext<TContext>(healthChecksBuilder)` | Adds a readiness health check for `TContext` |
| `AddDbMigratorService<TDbContext>(services)` | Registers the background migration service |
| `MigrateEfContexts<TDbContext>(applicationBuilder)` | Eagerly migrates all registered instances of `TDbContext` at startup |

## Usage

### 1. Define an entity

```csharp
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

[DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L10)]
public class Order : Model
{
    public required string CustomerName { get; set; }
    public decimal Total { get; set; }
}
```

### 2. Define an auditable entity with history

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;

[DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L10)]
public class Product : AuditableModel, IHistory
{
    public required string Name { get; set; }
    public decimal Price { get; set; }

    [ExcludeFromHistory]
    public string InternalNote { get; set; }
}
```

### 3. Create a DbContext

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : EfCoreDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("shop");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopDbContext).Assembly);

        // Apply NanoId key lengths and FK sizes
        modelBuilder.ApplyKeys();

        // Apply audit field defaults and foreign keys (pass the "user" entity type)
        modelBuilder.ApplyAuditFields(typeof(User), typeof(User));

        // Create history tables for all IHistory entities
        modelBuilder.ApplyHistories(typeof(User));

        // Rename all tables and columns to snake_case
        modelBuilder.ApplySnakeCaseNamingConventions();

        base.OnModelCreating(modelBuilder);
    }
}
```

### 4. Register the DbContext in your plugin

```csharp
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Plugins.Persistence.EfCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Configuration("shop:db")]
public class ShopDbConfiguration
{
    public string ConnectionString { get; set; } = null!;
}

public class ShopPlugin(ShopDbConfiguration config) : IPluginServiceConfiguration, IPluginApplicationConfiguration
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<ShopDbContext>(options =>
            options.UseNpgsql(config.ConnectionString));

        // Register the background migrator service instead of calling Migrate() on startup
        services.AddDbMigratorService<ShopDbContext>();
    }

    public void ConfigureApplication(IApplicationBuilder app)
    {
        // Or migrate eagerly at startup:
        // app.MigrateEfContexts<ShopDbContext>();
    }
}
```

> **Note:** `EfCorePlugin` must also be in scope (i.e., registered via `DosaicPluginTypes.All`) so that the trigger/interceptor infrastructure is wired up automatically.

### 5. Query data via `IDb`

```csharp
public class OrderService(IDb db)
{
    // Read-only query (AsNoTracking, expression-optimised)
    public Task<List<Order>> GetOpenOrdersAsync(CancellationToken ct)
        => db.GetQuery<Order>()
             .Where(o => o.Total > 0)
             .ToListAsync(ct);

    // Tracked set for mutations
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        await db.Get<Order>().AddAsync(order, ct);
        await db.SaveChangesAsync(ct);
    }
}
```

### 6. Stream and process large result sets

```csharp
// Process rows one-by-one without loading all into memory
var count = await db.GetQuery<Order>()
    .Where(o => o.Total > 100)
    .ProcessAsync(async (order, ct) =>
    {
        await SendInvoiceAsync(order, ct);
    }, cancellationToken);
```

### 7. Implement a before/after trigger

Triggers are discovered and registered automatically by `EfCorePlugin`.

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;

[TriggerOrder(Order = 10)]
public class OrderValidationTrigger : IBeforeTrigger<Order>
{
    public Task HandleBeforeAsync(ITriggerContext<Order> context, CancellationToken cancellationToken)
    {
        foreach (var change in context.ChangeSet)
        {
            if (change.Entity.Total < 0)
                throw new InvalidOperationException("Order total cannot be negative.");
        }
        return Task.CompletedTask;
    }
}

[TriggerOrder(Order = 20)]
public class OrderNotificationTrigger(INotificationService notifications) : IAfterTrigger<Order>
{
    public async Task HandleAfterAsync(ITriggerContext<Order> context, CancellationToken cancellationToken)
    {
        foreach (var change in context.ChangeSet.Where(c => c.State == ChangeState.Added))
            await notifications.SendOrderConfirmationAsync(change.Entity, cancellationToken);
    }
}
```

### 8. Implement business logic interception

`IBusinessLogic<T>` implementations are discovered automatically.

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;

public class ProductBusinessLogic : IBusinessLogic<Product>
{
    public Task BeforeCreateAsync(Product model, CancellationToken cancellationToken)
    {
        model.CreatedUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task BeforeUpdateAsync(Product model, CancellationToken cancellationToken)
    {
        if (model.Price < 0)
            throw new InvalidOperationException("Price cannot be negative.");
        return Task.CompletedTask;
    }
}
```

### 9. Implement event sourcing

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;

public enum InventoryEventType { Received, Shipped, Adjusted }

[DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L10)]
public class InventoryEvent : AggregateEvent<InventoryEventType>
{
    [EventMatcher] public string ProductId { get; set; }
}

// Processor is auto-registered by EfCorePlugin
public class InventoryEventProcessor : IEventProcessor<InventoryEvent>
{
    public async Task ProcessEventsAsync(IDb db, ImmutableArray<InventoryEvent> events, CancellationToken ct)
    {
        foreach (var evt in events)
        {
            var patch = AggregatePatch.FromJson(evt.EventData);
            // apply patch to read model ...
        }
    }
}

// Query all valid events for a given product
var events = await db.GetEvents(
    new InventoryEvent { Id = NanoId.Empty, ProductId = "product-123", EventData = "", ValidFrom = DateTime.UtcNow },
    dateTimeProvider);
```

### 10. Customise OpenTelemetry tracing

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions;
using OpenTelemetry.Trace;

public class ShopEfCoreConfigurator : IEfCoreConfigurator
{
    public void ConfigureEntityFrameworkCoreInstrumentation(
        Action<EntityFrameworkInstrumentationOptions> options)
    {
        options(new EntityFrameworkInstrumentationOptions
        {
            SetDbStatementForText = true
        });
    }

    public void ConfigureOtelWithTracing(TracerProviderBuilder builder)
    {
        builder.AddSource("ShopService");
    }
}
```

## Appsettings.yml example (PostgreSQL)

```yaml
shop:
  db:
    connectionString: "Host=localhost;Port=5432;Database=shop;Username=postgres;Password=postgres"
```

