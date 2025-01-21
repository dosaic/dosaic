# Dosaic.Hosting.Generator



Dosaic.Hosting.Generator is a `dotnet source generator package` that allows `dotnet dev's` to `use the dosaic web host to discover available dosaic plugins`.

## Installation

To install the nuget package follow these steps:

```shell

dotnet add package Dosaic.Hosting.Generator # this is required so the web host can discover & load the plugins
dotnet add package Dosaic.Hosting.WebHost
```

or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Hosting.Generator" Version="" />
<PackageReference Include="Dosaic.Hosting.WebHost" Version="" />
```

## Concept

The source generator will scan all assemblies which are referenced by the Dosaic.Hosting.Webhost project and add any classes or interfaces which are implementing

* the interface ```IPluginActivateable```
* any class attribute which starts with the name ```Dosaic```

to generate a class like the following example

```c#
using System;
using System.Diagnostics.CodeAnalysis;
using System.CodeDom.Compiler;
namespace Dosaic.Generated;

[ExcludeFromCodeCoverage]
[GeneratedCode("Dosaic.Hosting.Generator", "0.5.0.0")]
public class DosaicPluginTypes {
 public static Type[] All = new Type[] {
  typeof(Dosaic.Hosting.WebHost.Sample.HangfireTestJob),
  typeof(Dosaic.Hosting.WebHost.Sample.WebHostSamplePlugin),
  typeof(Dosaic.Hosting.WebHost.Sample.NpgsqlDbConfiguration),
  typeof(Dosaic.Api.OpenApi.OpenApiConfiguration),
  typeof(Dosaic.Api.OpenApi.OpenApiPlugin),
  typeof(Dosaic.Plugins.Authorization.Keycloak.KeycloakPlugin),
  typeof(Dosaic.Plugins.Authorization.Keycloak.KeycloakPluginConfiguration),
  typeof(Dosaic.Plugins.Endpoints.RestResourceEntity.RestResourceEntityPlugin),
  typeof(Dosaic.Plugins.Jobs.Hangfire.HangfireConfiguration),
  typeof(Dosaic.Plugins.Jobs.Hangfire.HangFirePlugin),
  typeof(Dosaic.Plugins.Persistence.EntityFramework.EntityFrameworkPlugin),
  typeof(Dosaic.Hosting.Abstractions.Middlewares.ExceptionMiddleware),
  typeof(Dosaic.Hosting.Abstractions.Middlewares.RequestContentLengthLimitMiddleware),
  typeof(Dosaic.Plugins.Handlers.Cqrs.SimpleResource.CqrsSimpleResourcePlugin),
 };
}
```

This class then gets referenced by the webhost's Program.cs e.g.

**If you don't include the generator package, you must specify the allowed plugin types manually!**

```csharp
using Dosaic.Hosting.WebHost;
PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

The webhost's service-, host- and application-configurators then access this list of types to activate and load all the found plugins on startup.

### Excludes

The following namespaces will be ignored when the source generator goes through all the assemblies at build time:

* Microsoft
* System
* FastEndpoints
* testhost
* netstandard
* Newtonsoft
* mscorlib
* NuGet
* NSwag
* FluentValidation
* YamlDotNet
* Accessibility
* NJsonSchema
* Namotion"

Changes to this list may happen in the future based on new requirements.
