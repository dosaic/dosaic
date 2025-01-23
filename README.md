<div style="display: flex; justify-content: center; align-items: center;margin-top:10px">
  <a href="https://dosaic.gitbook.io/dosaic/" target="_blank" style="display: flex; align-items: center;">
    <picture>
      <source srcset="https://raw.githubusercontent.com/dosaic/dosaic/HEAD/.gitbook/assets/logo.svg .gitbook/assets/logo.svg, ">
      <!-- <source srcset=".gitbook/assets/logo.svg"> -->
      <img alt="Dosaic" src=".gitbook/assets/logo.svg" height="64" width="64">
    </picture>
    <span style="margin-left: 10px; font-size: 2em; color: white;">Dosaic</span>
  </a>
</div>

<div style="display: flex; justify-content: center; align-items: center;margin-top:10px">
  A plugin-first dotnet framework for rapidly building anything hosted in the web.
</div>

<div style="display: flex; justify-content: center; align-items: center;margin-top:10px">

![Framework](https://img.shields.io/badge/framework-net9.0-blueviolet?style=flat-square)
[![MIT License](https://img.shields.io/badge/license-MIT-%230b0?style=flat-square)](https://github.com/dosaic/dosaic/blob/main/LICENSE.txt)
![Nuget](https://img.shields.io/nuget/v/Dosaic.Hosting.Webhost?style=flat-square)
![Nuget](https://img.shields.io/nuget/dt/Dosaic.Hosting.Webhost?style=flat-square)

</div>

## Documentation

For full documentation, visit https://dosaic.gitbook.io/dosaic/.

**Offline?**
Check the individual README files in each plugins directory.

## Features

* Supports structured logging with Serilog
* Exposes Prometheus compatible metrics endpoint
* Configurable trace sampling rates with OpenTelemetry
* Compatible with ASP.NET Core Minimal APIs & ASP.NET Core Mvc
* Uses source-generators to load plugins at runtime
* Optimized for high performance
* Utilizes Span<T> and Memory<T> for reduced allocations
* Implements OWASP security guidelines
* Follows .NET Core dependency injection and middleware patterns

## Prerequisites

Before you begin, ensure you have met the following requirements:

* You have installed the latest version of `.net9 sdk`

## Get started

To start using Dosaic components/plugins, you have to:

1. Install/add the PluginWebHost via nuget package `Dosaic.Hosting.WebHost`
2. Install/add the source generator plugin via nuget package `Dosaic.Hosting.Generator`
3. Rewrite the Entrypoint `Program.cs` to have the following code:

```c#
using Dosaic.Hosting.WebHost;
PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

4. Install/add your plugins via nuget packages prefixed with `Dosaic`


## Building Dosaic

To build Dosaic, follow these steps:

```sh
dotnet build ./Dosaic.sln
```

To format and style check the solution run:

```sh
dotnet format ./Dosaic.sln
```

## Testing Dosaic

We are using these frameworks for unit testing

* AutoBogus
* Bogus
* AwesomeAssertions
* Microsoft.NET.Test.Sdk
* NaughtyStrings.Bogus
* NSubstitute
* NUnit
* RichardSzalay.MockHttp
* WireMockDotNet

To run unit tests for Dosaic, follow these steps:

```
dotnet test ./Dosaic.sln
```

## Contributing to Dosaic

To contribute to Dosaic, follow these steps:

1. Check the issues if your idea or problem already exists
2. Open a new issue if necessary to explain your idea or problem with as much details as possible

To contribute code along your issue please follow the these steps:
2. Clone this repository.
3. Create a branch: `git checkout -b <branch_name>`.
4. Check our used tools and frameworks to ensure you are using the same tools which are already in place.
5. Check if your changes are in line with our style settings from .editorconfig and run `dotnet format`.
6. Make your changes and commit them: `git commit -m '<commit_message>'`
7. Push to the original branch: `git push origin Dosaic/<branch_name>`
8. Create the pull request.

## Development decisions

* Follow the code style which is configured via `.editorconfig`

### Disabled Compiler Errors

| Code | Reason                                      | Link                                                                                               |
| ---- | ------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| 1701 | Assembly Referencing                        | [1701](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1701) |
| 1702 | Assembly Referencing                        | [1702](https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs1702)                                 |
| 1591 | Disable XML Comments                        | [1591](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1591) |

### Implicit package overrides

#### System.Net.Http 4.3.0 -> 4.3.4

4.3.0 has some major security issues

* [Advisory: github.com (Severity: High)](https://github.com/advisories/GHSA-7jgj-8wvc-jh57)
* [Advisory by Mend.io: GHSA-7jgj-8wvc-jh57 (Severity: High)](https://osv.dev/vulnerability/GHSA-7jgj-8wvc-jh57)

Because of this, we decided to explicitly install a newer version,
which does not have the security issues.

Affected projects:
* Plugins.Persistence.Abstractions (through QDataQueryHelper.Core)
* Plugins.Persistence.MongoDb (through MongoDbMigrations)
* Extensions.RestEase.Tests (through WireMock.Net)

#### System.Text.RegularExpressions 4.3.0 -> 4.3.1

4.3.0 has some major security issues

* [Advisory: github.com (Severity: High)](https://github.com/advisories/GHSA-cmhx-cq75-c4mj)
* [Advisory by Mend.io: GHSA-cmhx-cq75-c4mj (Severity: High)](https://osv.dev/vulnerability/GHSA-cmhx-cq75-c4mj)

Because of this, we decided to explicitly install a newer version,
which does not have the security issues.

Affected projects:
* Plugins.Persistence.Abstractions (through QDataQueryHelper.Core)
* Plugins.Persistence.MongoDb (through MongoDbMigrations)
* Extensions.RestEase.Tests (through WireMock.Net)

#### Newtonsoft.Json 9.0.1 -> 13.0.3

9.0.1 has some major security issues

* [Advisory: github.com (Severity: High)](https://github.com/advisories/GHSA-5crp-9r3c-p9vr)
* [Advisory by Mend.io: GHSA-5crp-9r3c-p9vr (Severity: High)](https://osv.dev/vulnerability/GHSA-5crp-9r3c-p9vr)

Because of this, we decided to explicitly install a newer version,
which does not have the security issues.

Affected projects:
* Plugins.Persistence.MongoDb (through MongoDbMigrations)

#### SSH.NET 2020.0.1 -> 2024.2.0

2020.0.1 has some major security issues

* [Advisory: github.com (Severity: Moderate)](https://github.com/advisories/GHSA-72p8-v4hg-v45p)
* [Advisory by Mend.io: GHSA-72p8-v4hg-v45p (Severity: Moderate)](https://osv.dev/vulnerability/GHSA-72p8-v4hg-v45p)

Because of this, we decided to explicitly install a newer version,
which does not have the security issues.

Affected projects:
* Plugins.Persistence.MongoDb (through MongoDbMigrations)


## About us

This framework was based on https://github.com/sia-digital/pibox.

### Where did the name come from?

D - Dotnet

O - Orchestration

S - Services

A - Abstraction

I - Integration

C - Configuration

Just kidding, we were looking around and found **mosaic**, which is already kind of taken.
Then we just switched the first letter and everything made sense :D
