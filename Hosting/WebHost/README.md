# Dosaic.Hosting.Webhost


![Nuget](https://img.shields.io/nuget/v/Dosaic.Hosting.Webhost?style=flat-square)
![Nuget](https://img.shields.io/nuget/dt/Dosaic.Hosting.Webhost?style=flat-square)



Dosaic.Hosting.WebHost is the `core package` that allows `dotnet dev's` to `use the dosaic web host and dosaic plugins`.

**This package is mandatory, without it you can't use any plugins**

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

Rewrite the Entrypoint Program.cs to have following code:

```csharp
using Dosaic.Hosting.WebHost;
PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```


## Config files and ENV vars

Dosaic will try to load config files and values in the following order

1. `appsettings.json`
2. `appsettings.yaml`
3. `appsettings.yml`
4. `appsettings.*.json`
5. `appsettings.*.yaml`
6. `appsettings.*.yml`
7. `appsettings.secrets.yml`
8. `appsettings.*.secrets.yml`
9. `ENV variables`

**NOTE:**
All settings (does not matter which file extension) will be ordered by node length. And the secret files will be loaded as last (except the environment variables).

### Additional Config Paths

You can load config files from extra folders. Dosaic will scan them first, before the main app folder.

**Via Environment Variable:**

```shell
DOSAIC_HOST_ADDITIONALCONFIGPATHS=/path/to/configs,/another/path
```

**Via Command Line:**

```shell
dotnet run --additional-config-paths "/path/to/configs,/another/path"
```

Features:
- Scans subfolders for `appsettings.*` files
- Supports JSON, YAML, and YML formats
- Uses absolute or relative paths
- Non-existent paths are ignored
- Secrets files load last

Example:

1. appsettings.yaml (from extra paths)
2. appsettings.api.yaml (from extra paths)
3. appsettings.api.host.yaml (from extra paths)
4. appsettings.secrets.yaml (from extra paths)
5. appsettings.yaml (from main app)
6. appsettings.api.yaml (from main app)
7. appsettings.api.host.yaml (from main app)
8. appsettings.*.secrets.yaml (all locations)
9. ENV Variables

File names must always start with `appsettings` or they will be ignored!

Nested settings use `_` to build their hierarchy as ENV variables

```yaml
host:
  urls: http://+:5300 # optional, default is 8080;separate multiple urls with ","
```

becomes

```shell
HOST_URLS=http://+:5300 # optional, default is 8080;separate multiple urls with ","
```
```

## General settings

Configure your config file with these properties

```yaml
host:
  urls: http://+:5300 # optional, default is 8080;separate multiple urls with ","
  maxRequestSize: 8388608 # optional, default is 8 MB

```

or as ENV variables

```shell
HOST_URLS=http://+:5300 # optional, default is 8080;separate multiple urls with ","
HOST_MAXREQUESTSIZE=8388608 # optional, default is 8 MB
```

## Logging

Configure your appsettings.logging.yml with these properties

```yaml
serilog:
  minimumLevel: Debug # or Warn or Info
  override:
    System: Error # or Warning or Information
    Microsoft: Error # or Warning or Information

```

or as ENV variables

```shell
SERIOLOG_minimumLevel=Debug
SERIOLOG_OVERRIDE_SYSTEM=Error
SERIOLOG_OVERRIDE_MICROSOFT=Error
```

## Usage

Rewrite your entrypoint Program.cs to have following code:

```csharp
Dosaic.Hosting.WebHostPluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

Now you can add additional plugins as nuget packages to your project and configure them via config files/settings and/or in your web host plugin

## OpenTelemetry settings

Dosaic uses open telemetry for it's tracing capabilities. Further info can be found here&#x20;

https://opentelemetry.io/docs/instrumentation/net/getting-started/

If there is a tracing host configured, the service will try to send any traces, metrics or logs to this host. it will also enrich the log messages with SpanIds and TraceIds.

```yaml
telemetry:
  host: http://localhost:3333
  protocol: grpc
  headers:
    - name: Authorization
      value: Bearer
```

### Metrics

Dosaic uses open telemetry for it's metrics capabilities. Further info can be found here&#x20;

[#metrics](../Abstractions/#metrics "mention")
