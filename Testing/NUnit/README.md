# Dosaic.Testing.NUnit

A batteries-included meta-package for testing Dosaic-based services. Instead of tracking down individual testing dependencies for every project, add this single package and get a complete, pre-configured testing setup.

## Installation

```bash
dotnet add package Dosaic.Testing.NUnit
```

## Included Packages

| Package | Version | Purpose |
|---|---|---|
| `NUnit` | 4.5.0 | Test framework |
| `Allure.NUnit` | 2.14.1 | Test reporting and attachments |
| `AwesomeAssertions` | 9.4.0 | Fluent assertion library (`.Should()`) |
| `NSubstitute` | 5.3.0 | Mocking framework |
| `NSubstitute.Analyzers.CSharp` | 1.0.17 | Static analysis for correct NSubstitute usage (analyzer only) |
| `Bogus` | 35.6.5 | Fake data generation |
| `NaughtyStrings.Bogus` | 3.0.0 | Naughty/edge-case strings provider for Bogus |
| `OpenTelemetry` | 1.15.0 | Distributed tracing and metrics primitives |
| `Microsoft.EntityFrameworkCore` | — | EF Core (required for EF assertion helpers) |
| `Microsoft.EntityFrameworkCore.Relational` | — | EF Core relational (required for migration assertions) |
| `Chronos.Net` | 2.0.24 | Date/time provider abstractions |
| `TngTech.ArchUnitNET.NUnit` | 0.13.2 | Architecture unit tests |

The package also carries transitive project references to `Dosaic.Hosting.Abstractions` (core plugin interfaces) and `Dosaic.DevTools.Seeding` (EF Core fake data seeder).

## Test Helpers

| Type | Namespace | Description |
|---|---|---|
| `FakeLogger<T>` | `Dosaic.Testing.NUnit.Assertions` | In-memory `ILogger<T>` that captures log entries for assertion |
| `TestMetricsCollector` | `Dosaic.Testing.NUnit` | Listens to a named `System.Diagnostics.Metrics` instrument and collects measurements |
| `TestMetricsCollectorAssertions` | `Dosaic.Testing.NUnit` | Fluent assertion extensions for `List<MetricMeasurement>` |
| `ActivityTestBootstrapper` | `Dosaic.Testing.NUnit` | Registers a global `ActivityListener` so `Activity` instances are sampled in tests |
| `TestingDefaults` | `Dosaic.Testing.NUnit` | Creates a pre-configured `ServiceCollection` / `ServiceProvider` with logging, date-time providers, and `GlobalStatusCodeOptions` |
| `CustomConfiguration` | `Dosaic.Testing.NUnit` | Fluent builder for in-memory `IConfiguration` instances |
| `BaseTestEntity` / `BaseSubTestEntity` | `Dosaic.Testing.NUnit.Models` | Reusable record types for tests that need a simple entity graph |
| `PluginServiceConfigurationAssertions` | `Dosaic.Testing.NUnit.Assertions` | Asserts that an `IPluginServiceConfiguration` registers expected services |
| `PluginEndpointConfigurationAssertions` | `Dosaic.Testing.NUnit.Assertions` | Asserts that an `IPluginEndpointsConfiguration` registers expected routes and HTTP methods |
| `EntityTypeAssertions` / `DbContextAssertions` | `Dosaic.Testing.NUnit.Assertions` | Fluent assertions for EF Core entity type mappings, migrations, indexes, foreign keys, and seed data |
| `TracingAssertions` | `Dosaic.Testing.NUnit.Assertions` | Asserts that a `TracerProvider` has registered expected activity sources and instrumentations |
| `ArgExt` | `Dosaic.Testing.NUnit.Extensions` | NSubstitute argument matcher that delegates matching to an AwesomeAssertions assertion action |
| `ArchitectureExtensions` | `Dosaic.Testing.NUnit.Extensions` | Helpers for loading `Architecture` instances from the current or specific assemblies (ArchUnitNET) |
| `ObjectExtensions` | `Dosaic.Testing.NUnit.Extensions` | Reflection helper to read private/internal fields or properties by name |

## Usage

### FakeLogger&lt;T&gt;

Capture and assert on log entries without a real logging infrastructure.

```csharp
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.Extensions.Logging;

var logger = new FakeLogger<MyService>();
var service = new MyService(logger);

service.DoSomething();

logger.Entries.Should().ContainSingle(e =>
    e.Level == LogLevel.Information &&
    e.Message.Contains("expected message"));
```

### TestMetricsCollector

Listen to a named `System.Diagnostics.Metrics` instrument and verify emitted measurements.

```csharp
using Dosaic.Testing.NUnit;

using var metricsCollector = new TestMetricsCollector("my-metric-name");
metricsCollector.CollectedMetrics.Should().BeEmpty();

// exercise the code under test
await service.ProcessAsync();

metricsCollector.Instruments.Should().Contain("my-metric-name");

// assert a measurement with value 1
metricsCollector.CollectedMetrics.Should().ContainsMetric(1);

// assert with a specific tag
metricsCollector.CollectedMetrics.Should().ContainsMetric(1, "status", "ok");

// assert with multiple tags
metricsCollector.CollectedMetrics.Should().ContainsMetric(
    1,
    new[] { new KeyValuePair<string, string>("status", "ok") });

// sum all measurements (useful for gauges triggered via RecordObservableInstruments)
metricsCollector.RecordObservableInstruments();
metricsCollector.GetSum().Should().Be(42);
```

### ActivityTestBootstrapper

Enable `Activity` recording in tests (call once, e.g. in `[OneTimeSetUp]`).

```csharp
using Dosaic.Testing.NUnit;
using NUnit.Framework;

[SetUpFixture]
public class TestBootstrap
{
    [OneTimeSetUp]
    public void Setup() => ActivityTestBootstrapper.Setup();
}
```

### TestingDefaults

Get a pre-wired `ServiceCollection` or `ServiceProvider` including logging and Chronos date-time providers.

```csharp
using Dosaic.Testing.NUnit;

// obtain a ServiceProvider with defaults
var sp = TestingDefaults.ServiceProvider();

// or start from the collection and register additional services
var sc = TestingDefaults.ServiceCollection();
sc.AddSingleton<IMyService, MyService>();
var provider = sc.BuildServiceProvider();
```

### CustomConfiguration

Build an `IConfiguration` from key/value pairs without touching the file system.

```csharp
using Dosaic.Testing.NUnit;

var config = CustomConfiguration.Create()
    .Add("Database:Host", "localhost")
    .Add("Database:Port", "5432")
    .Build();

// use the empty singleton where no config values are needed
IConfiguration empty = CustomConfiguration.Empty;
```

### PluginServiceConfigurationAssertions

Verify that a plugin registers the expected services into the DI container.

```csharp
using Dosaic.Testing.NUnit.Assertions;

var plugin = new MyPlugin(/* dependencies */);

plugin.ShouldHaveServices(new Dictionary<Type, Type>
{
    { typeof(IMyService), typeof(MyService) },
    { typeof(IMyRepository), null }, // null = only check that it is registered
});
```

### PluginEndpointConfigurationAssertions

Verify that a plugin registers the expected routes and HTTP methods.

```csharp
using System.Net.Http;
using Dosaic.Testing.NUnit.Assertions;

var plugin = new MyEndpointsPlugin(/* dependencies */);

plugin.ShouldHaveEndpoints(new Dictionary<string, HttpMethod[]>
{
    { "/api/items", new[] { HttpMethod.Get, HttpMethod.Post } },
    { "/api/items/{id}", new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete } },
});

// retrieve a single endpoint for further assertions
var endpoint = plugin.GetEndpoint(HttpMethod.Get, "/api/items");
endpoint.Should().NotBeNull();
```

### EF Core Assertions (EntityTypeAssertions / DbContextAssertions)

Assert EF Core entity mappings, migration state, and seed data.

```csharp
using AwesomeAssertions;
using Dosaic.Testing.NUnit.Assertions;

// entity type mapping assertions
var entityType = dbContext.Model.FindEntityType(typeof(Customer))!;

entityType.Should().BeOnTable("public", "customers");
entityType.Should().HavePrimaryKey<Guid>("Id");
entityType.Should().HaveProperty<string>("Name", isNullable: false, maxLength: 200);
entityType.Should().HaveProperty<int>("Age");
entityType.Should().HaveIndex("Email", isUnique: true);
entityType.Should().HaveForeignKey("OrderId", typeof(Order));
entityType.Should().HasCheckConstraint("chk_age", "age > 0");
entityType.Should().HaveData(new[] { new Customer { Id = Guid.Parse("..."), Name = "Seed" } });

// migration assertions (no pending model changes)
dbContext.Should().MatchMigrations<MyDbContext>();

// compiled model assertion
dbContext.Should().MatchCompiledModel(MyCompiledModel.Instance);
```

### TracingAssertions

Verify that a `TracerProvider` (built during plugin configuration) has registered the correct sources and instrumentations.

```csharp
using Dosaic.Testing.NUnit.Assertions;

var sc = TestingDefaults.ServiceCollection();
// configure plugin which adds OpenTelemetry tracing
plugin.ConfigureServices(sc);
var sp = sc.BuildServiceProvider();

sp.Should().RegisterSources("MyService.ActivitySource");
sp.Should().RegisterSources(new[] { "Source1", "Source2" });
sp.Should().RegisterInstrumentation<HttpClientInstrumentation>();
sp.Should().RegisterInstrumentation("AspNetCoreInstrumentation");
```

### ArgExt (NSubstitute argument matching with AwesomeAssertions)

Use fluent assertion lambdas as NSubstitute argument matchers.

```csharp
using Dosaic.Testing.NUnit.Extensions;
using NSubstitute;

var repository = Substitute.For<IOrderRepository>();

await service.PlaceOrderAsync(new Order { CustomerId = 42, Total = 99.99m });

await repository.Received(1).SaveAsync(
    ArgExt.Is<Order>(order =>
    {
        order.CustomerId.Should().Be(42);
        order.Total.Should().Be(99.99m);
    }));
```

### ArchitectureExtensions

Load an `Architecture` from the calling assembly or a set of specific assemblies for use with ArchUnitNET.

```csharp
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Dosaic.Testing.NUnit.Extensions;
using NUnit.Framework;

[TestFixture]
public class ArchitectureTests
{
    private static readonly Architecture _architecture =
        new ArchLoader().FromCurrentAssembly();

    [Test]
    public void ServicesShouldNotDependOnControllers()
    {
        var services = ArchRuleDefinition.Classes()
            .That().ResideInNamespace("MyApp.Services");

        var controllers = ArchRuleDefinition.Classes()
            .That().ResideInNamespace("MyApp.Controllers");

        services.Should().NotDependOnAny(controllers).Check(_architecture);
    }
}
```

### ObjectExtensions

Read private or internal fields and properties by name via reflection (useful when testing internal state without exposing it).

```csharp
using Dosaic.Testing.NUnit.Extensions;

var service = new MyService();
// access a private field named "_cache"
var cache = service.GetInaccessibleValue<Dictionary<string, object>>("_cache");
cache.Should().BeEmpty();
```
