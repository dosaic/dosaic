<!-- # Dosaic -->

<div style="display: flex; justify-content: center; align-items: center;margin-top:10px">
  <a href="https://dosaic.gitbook.io/dosaic/" target="_blank" style="display: flex; align-items: center;">
    <picture>
      <source srcset="https://raw.githubusercontent.com/dosaic/dosaic/HEAD/.gitbook/assets/logo.svg .gitbook/assets/logo.svg, ">
      <!-- <source srcset=".gitbook/assets/logo.svg"> -->
      <img alt="Dosaic" src=".gitbook/assets/logo.svg" height="64">
    </picture>
    <span style="margin-left: 10px; font-size: 2em; color: white;">Dosaic</span>
  </a>
</div>

<p align="center">
  A plugin-first dotnet framework for rapidly building anything hosted in the web.
</p>

<p align="center">

![Framework](https://img.shields.io/badge/framework-net8.0-blueviolet?style=flat-square)
[![MIT License](https://img.shields.io/badge/license-MIT-%230b0?style=flat-square)](https://github.com/dosaic/dosaic/blob/main/LICENSE.txt)
![Nuget](https://img.shields.io/nuget/v/Dosaic.Hosting.Webhost?style=flat-square)
![Nuget](https://img.shields.io/nuget/dt/Dosaic.Hosting.Webhost?style=flat-square)

</p>

## Documentation

For full documentation, visit https://dosaic.gitbook.io/dosaic/.

**Offline?**
Check the individual README files in each plugins directory.

## Features

* logs
* metrics
* traces
* minimal api ready
* auto discovered plugins
* with performance in mind
* less memory allocations
* industry best practices applied security
* standard dotnet core patterns & APIs

## Prerequisites

Before you begin, ensure you have met the following requirements:

* You have installed the latest version of `.net8 sdk`


## Get started

To start using Dosaic components/plugins, you have to:

1. Install/add the PluginWebHost via nuget package `Dosaic.Hosting.WebHost`
2. Install/add the source generator plugin via nuget package `Dosaic.Hosting.Generator`
3. Rewrite the Entrypoint `Program.cs` to have following code:

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
* FluentAssertions
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


## About us

### Where did the name come from?

D - Dotnet

O - Orchestration

S - Services

A - Abstraction

I - Integration

C - Configuration

Just kidding, we were looking around and found **mosaic**, which is already kind of taken.
Then we just switched the first letter and everything made sense :D
