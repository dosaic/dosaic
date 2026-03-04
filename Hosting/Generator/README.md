# Dosaic.Hosting.Generator

`Dosaic.Hosting.Generator` is a Roslyn `IIncrementalGenerator` that runs at **compile time** to discover all Dosaic plugin types across the application and its referenced assemblies. It emits a single generated class — `Dosaic.Generated.DosaicPluginTypes` — containing a `Type[]` of every discovered plugin. This enables AOT-compatible, reflection-free plugin discovery for `Dosaic.Hosting.WebHost`.

## Installation

Add both packages to your application project:

```shell
dotnet add package Dosaic.Hosting.Generator
dotnet add package Dosaic.Hosting.WebHost
```

Or as `PackageReference` entries in your `.csproj`:

```xml
<PackageReference Include="Dosaic.Hosting.Generator" Version="" />
<PackageReference Include="Dosaic.Hosting.WebHost" Version="" />
```

> **Note:** If you omit `Dosaic.Hosting.Generator` you must pass plugin types to the web host manually.

## Usage

Because the generator runs during compilation, no additional configuration is required. Reference `DosaicPluginTypes.All` directly in your `Program.cs`:

```csharp
using Dosaic.Hosting.WebHost;

PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

`PluginWebHostBuilder` iterates the provided `Type[]` and activates every service-, application-, and endpoint-configurator plugin at startup.

## How it Works

`PluginTypesGenerator` implements `IIncrementalGenerator` and executes the following pipeline on every incremental build:

1. **Syntax filtering** — registers a `SyntaxProvider` that selects every `TypeDeclarationSyntax` node in the compilation.
2. **Type resolution** — combines the full `Compilation` with the collected syntax nodes and resolves each candidate to an `ITypeSymbol`.
3. **Discovery** — scans both the **host assembly** and every **referenced assembly symbol** for types that match the inclusion criteria (see below).
4. **Code emission** — writes `DosaicPluginTypes.g.cs` into the compilation with the `Dosaic.Generated` namespace.

### Inclusion criteria

A type is included in `DosaicPluginTypes.All` when **all** of the following are true:

- The type is `public` and **not** `abstract`.
- The root namespace of the type does **not** match any [excluded namespace prefix](#excluded-namespaces).
- At least one of:
  - The type (directly or via its interface hierarchy) implements `IPluginActivateable`, **or**
  - The type carries an attribute whose root namespace starts with `Dosaic`.

### Generated output

The generator emits a file named `DosaicPluginTypes.g.cs` similar to the following example:

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.CodeDom.Compiler;

namespace Dosaic.Generated;

[ExcludeFromCodeCoverage]
[GeneratedCode("Dosaic.Hosting.Generator", "1.0.0.0")]
public class DosaicPluginTypes
{
    public static Type[] All = new Type[]
    {
        typeof(Dosaic.Api.OpenApi.OpenApiConfiguration),
        typeof(Dosaic.Api.OpenApi.OpenApiPlugin),
        typeof(Dosaic.Hosting.Abstractions.Middlewares.ExceptionMiddleware),
        typeof(Dosaic.Hosting.Abstractions.Middlewares.RequestContentLengthLimitMiddleware),
        typeof(Dosaic.Plugins.Authorization.Keycloak.KeycloakPlugin),
        typeof(Dosaic.Plugins.Authorization.Keycloak.KeycloakPluginConfiguration),
        typeof(Dosaic.Plugins.Endpoints.RestResourceEntity.RestResourceEntityPlugin),
        typeof(Dosaic.Plugins.Jobs.Hangfire.HangfireConfiguration),
        typeof(Dosaic.Plugins.Jobs.Hangfire.HangFirePlugin),
        typeof(Dosaic.Plugins.Persistence.EntityFramework.EntityFrameworkPlugin),
    };
}
```

Types are sorted alphabetically by their fully-qualified name. Generic types are emitted as open generics (e.g. `typeof(MyPlugin<>)`).

When no plugin types are found the generator still emits the class with an empty array:

```csharp
public static Type[] All = new Type[0];
```

### Excluded namespaces

The following root-namespace prefixes are ignored during discovery to avoid pulling in framework or third-party types:

| Prefix | Reason |
|---|---|
| `Microsoft` | ASP.NET Core / runtime types |
| `System` | BCL types |
| `FastEndpoints` | Third-party endpoint library |
| `testhost` | Test runner host |
| `netstandard` | .NET Standard contract assembly |
| `Newtonsoft` | JSON.NET |
| `mscorlib` | Legacy BCL |
| `NuGet` | NuGet client types |
| `NSwag` | OpenAPI tooling |
| `FluentValidation` | Validation library |
| `YamlDotNet` | YAML parsing |
| `Accessibility` | Windows UI accessibility |
| `NJsonSchema` | JSON schema library |
| `Namotion` | JSON schema / reflection helpers |

> This list may be extended in future releases as new dependencies are integrated.

## Features

- **Zero runtime reflection** — all plugin discovery happens at Roslyn compile time; no assembly scanning at startup.
- **Incremental generation** — uses `IIncrementalGenerator` for fast, cache-friendly incremental builds; only re-runs when affected syntax changes.
- **Full-assembly scanning** — discovers plugins in both the host application assembly and every transitively referenced assembly.
- **Generic type support** — correctly emits open generic `typeof(…)` expressions for generic plugin types.
- **AOT compatible** — the generated `Type[]` is a plain static field; compatible with ahead-of-time compilation and trimming.
- **Automatic exclusion** — well-known framework and third-party namespaces are filtered out automatically.
- **Deterministic output** — types are sorted alphabetically by fully-qualified name, producing stable diffs across builds.
- **Decorated output** — generated file is annotated with `[ExcludeFromCodeCoverage]` and `[GeneratedCode]` to keep tooling metrics clean.
