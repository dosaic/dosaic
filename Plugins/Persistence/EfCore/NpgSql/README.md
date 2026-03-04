# Dosaic.Plugins.Persistence.EfCore.NpgSql

`Dosaic.Plugins.Persistence.EfCore.NpgSql` is a Dosaic plugin that integrates PostgreSQL into the EF Core persistence layer via the Npgsql provider. It provides connection and pool configuration, automatic PostgreSQL enum mapping via `[DbEnumAttribute]`, lambda injection support, and a hosted background migration service.

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.EfCore.NpgSql
```

Or add directly to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.EfCore.NpgSql" Version="" />
```

## Configuration

The plugin binds its configuration from the `npgsql` section via the `[Configuration("npgsql")]` attribute on `EfCoreNpgSqlConfiguration`.

### appsettings.yml

```yaml
npgsql:
  host: "localhost"
  port: 5432
  database: "mydb"
  username: "postgres"
  password: "postgres"
  connectionLifetime: 60        # seconds; 0 = indefinite (default: 60)
  keepAlive: 15                 # seconds; 0 = disabled (default: 15)
  maxPoolSize: 100              # (default: 100)
  configureLoggingCacheTimeInSeconds: 300  # (default: 300)
  splitQuery: false             # use SplitQuery behaviour (default: false)
  includeErrorDetail: false     # include PG error details — may expose sensitive data
  enableDetailedErrors: false   # EF Core detailed errors — may expose sensitive data
  enableSensitiveDataLogging: false  # log parameter values — may expose sensitive data
```

### Configuration class

```csharp
// Automatically resolved by Dosaic via [Configuration("npgsql")]
[Configuration("npgsql")]
public class EfCoreNpgSqlConfiguration
{
    public string Host { get; set; }
    public int Port { get; set; } = 5432;
    public string Database { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int ConnectionLifetime { get; set; } = 60;
    public int KeepAlive { get; set; } = 15;
    public int MaxPoolSize { get; set; } = 100;
    public int ConfigureLoggingCacheTimeInSeconds { get; set; } = 300;
    public bool IncludeErrorDetail { get; set; }
    public bool EnableDetailedErrors { get; set; }
    public bool EnableSensitiveDataLogging { get; set; }
    public bool SplitQuery { get; set; }
}
```

## Usage

### Registering a DbContext in a Dosaic plugin

`EfCoreNpgSqlConfiguration` is injected automatically by the Dosaic `TypeImplementationResolver`.
Use `ConfigureNpgSqlContext<TDbContext>` to wire up the Npgsql provider on your `DbContextOptionsBuilder`.

```csharp
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Plugins.Persistence.EfCore.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.NpgSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class MyPlugin(EfCoreNpgSqlConfiguration npgsqlConfig)
    : IPluginServiceConfiguration, IPluginHealthChecksConfiguration
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<MyDbContext>(
            (provider, options) => options.ConfigureNpgSqlContext<MyDbContext>(provider, npgsqlConfig));

        // Optional: run EF migrations in the background on startup
        services.AddNpgsqlDbMigratorService<MyDbContext>();
    }

    public void ConfigureHealthChecks(IHealthChecksBuilder healthChecks)
    {
        healthChecks.AddEfCoreContext<MyDbContext>();
    }
}
```

### Defining a DbContext

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;

public class MyDbContext(DbContextOptions<MyDbContext> options) : EfCoreDbContext(options)
{
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
        });

        // register any [DbEnum]-attributed enums for this context
        modelBuilder.MapDbEnums<MyDbContext>();

        base.OnModelCreating(modelBuilder);
    }
}
```

### PostgreSQL enum mapping

Decorate your C# enums with `[DbEnum]` to have them automatically mapped to PostgreSQL enum types.
`PostgresEnumExtensions` scans all enums bearing this attribute in the assembly of your `TDbContext`
(and in the EF Core Abstractions assembly) and registers them with the Npgsql provider.

```csharp
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;

[DbEnum("order_status", "public")]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
}
```

The enum values are translated to snake_case names (e.g. `Processing` → `processing`) automatically via `NpgsqlSnakeCaseNameTranslator`.

### Advanced: customising the Npgsql options builder

Pass an optional `Action<NpgSqlConfiguration>` to `ConfigureNpgSqlContext` to further customise the provider:

```csharp
services.AddDbContext<MyDbContext>((provider, options) =>
    options.ConfigureNpgSqlContext<MyDbContext>(provider, npgsqlConfig, c =>
    {
        // Provide a pre-built NpgsqlDataSource (e.g. for password rotation)
        c.WithDataSource(dataSourceBuilder =>
        {
            dataSourceBuilder.UsePeriodicPasswordProvider(/* ... */);
        });

        // Fine-tune the Npgsql EF builder
        c.WithNpgSql(npgsql =>
        {
            npgsql.CommandTimeout(60);
        });

        // Override EF Core warning behaviour
        c.WithWarnings(w =>
        {
            w.Log((CoreEventId.RowLimitingOperationWithoutOrderByWarning, LogLevel.Debug));
        });

        // Use a pre-compiled EF Core model (AOT / startup performance)
        c.WithModel(MyDbContextModel.Instance);
    }));
```

### Background migration service

`NpgsqlDbMigratorService<TDbContext>` is a hosted `BackgroundService` that:

1. Retrieves all pending EF Core migrations and applies them in order.
2. After a successful migration run, reloads the Npgsql type map so that any newly created PostgreSQL enum types are immediately usable.
3. Retries on failure with a 1-second delay.

Register it with:

```csharp
services.AddNpgsqlDbMigratorService<MyDbContext>();
```

## Features

- **Npgsql EF Core provider** — connects to PostgreSQL using `Npgsql.EntityFrameworkCore.PostgreSQL` with a fully configured `NpgsqlDataSource`
- **Connection pool tuning** — configurable `MaxPoolSize`, `ConnectionLifetime`, `KeepAlive`, and `ArrayNullabilityMode=PerInstance`
- **Automatic PostgreSQL enum mapping** — `[DbEnumAttribute]` marks C# enums; `MapDbEnums<TDbContext>` registers them on both the data source builder and the model builder with snake_case translation
- **Query splitting** — toggle `SplitQuery` to switch between `SingleQuery` and `SplitQuery` behaviour globally
- **Lambda injection** — `NeinLinq` lambda injection is enabled automatically via `WithLambdaInjection()`
- **Configurable logging** — logging cache time, detailed errors, and sensitive data logging are all controlled through configuration
- **Background migrator** — `NpgsqlDbMigratorService<TDbContext>` applies pending migrations on startup and reloads Npgsql types afterward
- **Health check integration** — use `AddEfCoreContext<TDbContext>` (from `Dosaic.Plugins.Persistence.EfCore.Abstractions`) to expose a readiness health check for the database context
- **OpenTelemetry instrumentation** — `OpenTelemetry.Instrumentation.EntityFrameworkCore` is included automatically

