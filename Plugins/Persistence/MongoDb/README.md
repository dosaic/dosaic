# Dosaic.Plugins.Persistence.MongoDb



Dosaic.Plugins.Persistence.MongoDb is a `plugin` that allows other `Dosaic components` to `use the MongoDb core to interact with certain databases`.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Persistence.MongoDb
```
or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.MongoDb" Version="" />
```

## Appsettings.yml

Configure your appsettings.yml with these properties

Postgres for example
```yaml
mongodb:
  host: "localhost"
  database: "mongodb"
  port: "27017"
  password: "mongodb"
  user: "mongodb"
```

MongoDbConfiguration.cs
```csharp
[Configuration("mongodb")]
public class MongoDbConfiguration
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Database { get; set; } = null!;
    public string AuthDatabase { get; set; } = null!;
    public string User { get; set; } = null!;
    public string Password { get; set; } = null!;
}
```

## Configuration in your plugin host

Nothing to configure

## Usage

### IMongoDbInstance
```csharp
public class ExampleService
{
    private readonly IMongoDbInstance _mongoDbInstance;

    public ExampleService(IMongoDbInstance mongoDbInstance)
    {
        _mongoDbInstance = mongoDbInstance;
    }
    public async Task DoStuff()
    {
        // for example
        var mongoCollection = _mongoDbInstance.GetCollectionFor<MyCollectionEntity>();
    }
}
```
