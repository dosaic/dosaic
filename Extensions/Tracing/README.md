# Dosaic.Extensions.Tracing

Compile-time OpenTelemetry tracing for Dosaic apps, woven by [Metalama](https://www.metalama.net/).
No runtime DI, no reflection, no proxies — activities are emitted directly into your methods at build time
and feed the shared `Dosaic` `ActivitySource`.

## Install

```xml
<ItemGroup>
  <PackageReference Include="Dosaic.Extensions.Tracing" Version="1.0.0" />
</ItemGroup>
```

Adding the reference is enough: a transitive Metalama fabric traces your code according to the MSBuild
properties below. No code changes required.

## Configuration (MSBuild)

| Property | Default | Meaning |
|---|---|---|
| `DosaicTracingMode` | `AllPublic` | `AllPublic` · `All` · `PublicAsync` · `AttributeOnly` |
| `DosaicTracingCaptureArgs` | `None` | Happy-path argument capture: `None` · `ToString` · `Json` |
| `DosaicTracingCaptureArgsOnError` | `ToString` | Argument capture applied **only when a method throws**: `None` · `ToString` · `Json` |
| `DosaicTracingInclude` | everything | Semicolon-separated namespace globs (`*` = one segment, `**` = any) |
| `DosaicTracingExclude` | nothing | Semicolon-separated namespace globs |

```xml
<PropertyGroup>
  <DosaicTracingMode>AllPublic</DosaicTracingMode>
  <DosaicTracingCaptureArgs>None</DosaicTracingCaptureArgs>
  <DosaicTracingCaptureArgsOnError>Json</DosaicTracingCaptureArgsOnError>
  <DosaicTracingInclude>MyApp.Features.*;MyApp.Services.*</DosaicTracingInclude>
  <DosaicTracingExclude>**.Migrations;**.Generated</DosaicTracingExclude>
</PropertyGroup>
```

> **Argument capture on error.** Even with `DosaicTracingCaptureArgs=None` (the default), arguments are
> captured onto the span when a method throws, controlled by `DosaicTracingCaptureArgsOnError` (default
> `ToString`). When happy-path capture is already on, the error capture is skipped to avoid duplicate tags.

## Attributes

- `[Trace]` — trace a single method, or every eligible instance method of a class.
  `[Trace(CaptureArgs = ArgCaptureMode.Json)]` overrides capture for that target.
- `[NoTrace]` — opt a class or method out of automatic tracing.
- `[NoCapture]` — opt a single parameter out of argument capture.

Parameters of these types are never captured: `CancellationToken`, `Stream`, `byte[]`,
`ReadOnlyMemory<byte>`, `IServiceProvider`, `ClaimsPrincipal`, `HttpContext`, `IFormFile`.

## Enriching the current span

`using Dosaic.Extensions.Tracing;` adds static extension members to `Dosaic.Hosting.Abstractions.Tracing`,
so you enrich `Activity.Current` directly off `Tracing` — no new activity is started:

```csharp
Tracing.Tag("property.id", id);
Tracing.Event("PropertyLoaded");
Tracing.Error(ex);
Tracing.Link(otherActivityContext);
```

`Tracing.Current` returns the ambient activity (or null). All calls are no-ops when no activity is active.

## Notes

- `Json` capture reuses Dosaic's shared `SerializationExtensions.Serialize` (`System.Text.Json`,
  camelCase, enums as strings, nulls ignored). It is **not** trimming / Native-AOT safe — prefer `ToString`
  under AOT.
- Argument capture never throws: if serialization fails (e.g. a reference cycle), the tag is set to
  `<ExceptionTypeName>` instead. A failed capture can never mask the method's own exception.
- Span names are `TypeName.MethodName`; all spans use the shared `Dosaic` `ActivitySource`.
