# Dosaic.Plugins.Caching.Redis



Dosaic.Plugins.Caching.Redis is a `plugin` that allows other `Dosaic components` to `use distributed cache with redis`.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Caching.Redis
```
or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Caching.Redis" Version="" />
```

## Appsettings.yml

Configure your appsettings.yml with these properties

Postgres for example
```yaml
redisCache:
  connectionString: "localhost:6379"
```

## Usage

```csharp
internal class TestService(IDistributedCache cache)
{
    public async Task DoStuff()
    {
        await cache.SetStringAsync("key", "value");
        var value = await cache.GetStringAsync("key");
    }
}
```


