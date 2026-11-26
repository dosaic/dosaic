# Dosaic.Extensions.Tracing

Opt-in OpenTelemetry spans for Dosaic apps, built on plain `System.Diagnostics.ActivitySource`, the native
.NET OpenTelemetry API. No weaver, no source generator, no reflection, no proxies: you start a span where it
tells you something, and it feeds the shared `Dosaic` `ActivitySource` that the Dosaic web host already
exports.

## Install

```xml
<ItemGroup>
  <PackageReference Include="Dosaic.Extensions.Tracing" />
</ItemGroup>
```

```csharp
using Dosaic.Extensions.Tracing;
using Dosaic.Hosting.Abstractions;
```

`using Dosaic.Extensions.Tracing;` adds static extension members to `Dosaic.Hosting.Abstractions.Tracing`.

## What is already traced

You do not need custom spans for infrastructure. The Dosaic web host and plugins register the OpenTelemetry
instrumentations for incoming HTTP requests (ASP.NET Core), outgoing HTTP calls (`HttpClient`), EF Core,
MongoDB, Redis and Hangfire. Every request already arrives with a trace and its database, cache and HTTP child
spans.

Add your own spans for **business operations**: steps that make a decision, call several dependencies, or
that you want to find and time in a trace (`ImportService.ImportBatch`, `PricingEngine.Calculate`). Do not
wrap every method: a span per getter, mapper or validator adds export cost and storage, and buries the spans
that matter.

## Starting a span

```csharp
public class ImportService
{
    public async Task<ImportResult> ImportBatchAsync(Batch batch, CancellationToken ct)
    {
        using var activity = Tracing.StartSpan(this);   // "ImportService.ImportBatchAsync"
        activity?.SetTag("batch.size", batch.Items.Count);
        ...
    }
}
```

- The span name is `{TypeName}.{MethodName}`: the runtime type of `this` (generic arguments rendered, e.g.
  `Repository<Order>`) and the calling member via `[CallerMemberName]`.
- `Tracing.StartSpan<TOwner>()` does the same in static members.
- `Tracing.StartSpan(this, kind: ActivityKind.Consumer)` sets the activity kind (default `Internal`).
- The activity is `null` when nothing listens to the `Dosaic` source (sampling off, no exporter), so use `?.`.

## Recording errors

`using var activity = ...` ends the span on an exception but does not mark it as failed. Wrap the body with
`Trace` / `TraceAsync` to mark the span as error (status + exception event) and re-throw:

```csharp
public Task<Price> CalculateAsync(Order order) => Tracing.TraceAsync(this, async activity =>
{
    activity?.SetTag("order.id", order.Id);
    return await _engine.PriceAsync(order);
});

public void Rebuild(string index) => Tracing.Trace(this, _ => _search.Rebuild(index));
```

Overloads: `Trace(owner, Action<Activity?>)`, `Trace<T>(owner, Func<Activity?, T>)`,
`TraceAsync(owner, Func<Activity?, Task>)`, `TraceAsync<T>(owner, Func<Activity?, Task<T>>)`.
The span name is built from the calling member, as with `StartSpan`.

## Enriching the current span

These work on `Activity.Current` and never start a new activity. They are no-ops when no activity is active,
so they are safe anywhere, including inside instrumented ASP.NET Core requests:

```csharp
Tracing.Tag("property.id", id);
Tracing.Event("PropertyLoaded");
Tracing.Error(ex);
Tracing.Link(otherActivityContext);
```

`Tracing.Current` returns the ambient activity (or null).

## Arguments

Arguments are not captured automatically. Tag what is useful and safe to export:

```csharp
activity?.SetTag("customer.id", customerId);
activity?.SetTag("filter", filter.Serialize());   // Dosaic's System.Text.Json settings
```

Never tag secrets, tokens, personal data or large payloads.

## Migrating from 1.2.x (Metalama)

Versions 1.2.x wove spans into methods at build time with Metalama. That is gone: the package no longer
references Metalama and no longer traces anything on its own. See the changelog entry for 11.0.0 for the full
list; in short:

1. Remove the `DosaicTracing*` MSBuild properties (`DosaicTracingMode`, `DosaicTracingCaptureArgs`,
   `DosaicTracingCaptureArgsOnError`, `DosaicTracingInclude`, `DosaicTracingExclude`) and any
   `PackageReference` to `Metalama.Framework` you only had for this package.
2. Remove `[Trace]`, `[NoTrace]` and `[NoCapture]`, and any use of `TracingMode` / `ArgCaptureMode`.
3. Add `using var activity = Tracing.StartSpan(this);` or `Tracing.Trace(Async)(this, ...)` to the methods
   whose spans you actually look at. Span names stay `TypeName.MethodName`, so existing dashboards and queries
   keep matching for the methods you instrument.
4. Replace argument capture with explicit `SetTag` calls where the value helps.
