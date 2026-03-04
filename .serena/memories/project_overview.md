# Dosaic — Project Overview

**Dosaic** is a plugin-first .NET framework for rapidly building anything hosted in the web.  
The name is a play on "mosaic" — Dotnet Orchestration Services Abstraction Integration Configuration.

## Tech Stack
- **.NET 10.0** (SDK 10.0.103 via global.json)
- **ASP.NET Core** Minimal APIs + MVC
- **Serilog** for structured logging
- **OpenTelemetry** for metrics, tracing, and log export
- **Prometheus** for metrics scraping
- Central Package Management (`Directory.Packages.props`)
- **Source Generators** for AOT-compatible plugin discovery

## Solution Layout
```
Hosting/
  Abstractions/    → Core plugin interfaces, attributes, services
  Generator/       → IIncrementalGenerator that emits DosaicPluginTypes.All
  WebHost/         → PluginWebHostBuilder, Configurators, TypeImplementationResolver
Plugins/
  Authorization/   → Keycloak integration
  Caching/         → Redis distributed cache
  Endpoints/       → REST resource entity abstractions
  Handlers/        → CQRS abstractions + implementation
  Jobs/            → Hangfire scheduler
  Management/      → Unleash feature flags
  Mapping/         → Mapster auto-mapping
  Messaging/       → MassTransit messaging
  Persistence/     → EF Core, MongoDB, InMemory, S3, VaultSharp
  Storage/         → SMB file storage
  Validations/     → FluentValidation + attribute-based
Extensions/
  Abstractions/    → Page, PagedList, CurrencyValue, Quantity
  NanoId/          → NanoId generation
  RestEase/        → HTTP client via RestEase
  Sqids/           → Sqids encoding
Testing/
  NUnit/           → Test helpers: FakeLogger, TestingDefaults, metrics collectors
example/           → Reference service showing usage
```

## Entry Point
```csharp
using Dosaic.Hosting.WebHost;
PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

## Configuration
- YAML + JSON support (appsettings.yml / appsettings.json)
- Layered: root → additional paths → secrets → env vars → CLI args
- `[Configuration("section")]` attribute binds config sections to types
- Kestrel defaults: port 8080, 8MB max body, no server header
