# Dosaic.Plugins.Mapping.Mapster

Dosaic.Plugins.Mapping.Mapster is a `plugin` that allows other `Dosaic components` to `use dto mapping using Mapster`.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Mapping.Mapster
```
or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Mapping.Mapster" Version="" />
```

## Appsettings.yml

You do not need to configure anything, because the implementation resolver, does this automatically at startup.

## Usage

Currently its only implemented to generate the Mapster mapping rules by the "MapFromAttribute" so you can inline all mappings.
If it is needed to do more, we need to implement it.

```csharp

Example:
```csharp
internal class DbModel {
    public string Id {get;set;}
    public string LongName {get;set;}
}

internal class SomeDto {
    public string Id {get;set;}
    [MapFrom<DbModel>(nameof(DbModel.LongName))]
    public string Name {get;set;}
}

// USAGE:
var dbModel = new DbModel { Id = "1", LongName = "LongName" };
var someDto = dbModel.Adapt<SomeDto>();
Console.WriteLine(someDto.Name); // "LongName"
```


