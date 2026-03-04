# Dosaic.Plugins.Persistence.MongoDb

`Dosaic.Plugins.Persistence.MongoDb` is a Dosaic plugin that integrates MongoDB into your application. It registers a singleton `IMongoDbInstance` for collection access, wires up OpenTelemetry distributed tracing, exposes driver-level metrics counters, and adds a readiness health check — all driven by a single configuration section.

## Installation

```shell
dotnet add package Dosaic.Plugins.Persistence.MongoDb
```

Or as a package reference in your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Persistence.MongoDb" Version="" />
```

## Configuration

### appsettings.yml

```yaml
mongodb:
  host: "localhost"
  port: 27017
  database: "mydb"
  authDatabase: ""      # optional — falls back to 'database' when empty
  user: "mongouser"
  password: "s3cr3t"
```

### appsettings.json

```json
{
  "mongodb": {
    "host": "localhost",
    "port": 27017,
    "database": "mydb",
    "authDatabase": "",
    "user": "mongouser",
    "password": "s3cr3t"
  }
}
```

### Configuration class

The `[Configuration("mongodb")]` attribute causes Dosaic's `TypeImplementationResolver` to automatically bind this section and inject the result as a constructor dependency.

```csharp
[Configuration("mongodb")]
public class MongoDbConfiguration
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Database { get; set; } = null!;
    /// <summary>
    /// The database used for authentication. Falls back to Database when empty.
    /// </summary>
    public string AuthDatabase { get; set; } = null!;
    public string User { get; set; } = null!;
    public string Password { get; set; } = null!;
}
```

> **Authentication is optional.** When `User` and `Password` are both empty the driver connects without credentials.

### docker-compose (local development)

A `docker-compose.yml` is included at the root of the plugin directory for quick local setup.

## Usage

### Accessing collections

Inject `IMongoDbInstance` and call `GetCollectionFor<T>()`. The collection name is derived from the type name (`typeof(T).Name`).

```csharp
public class OrderRepository
{
    private readonly IMongoCollection<Order> _orders;

    public OrderRepository(IMongoDbInstance mongoDb)
    {
        _orders = mongoDb.GetCollectionFor<Order>();
    }

    public async Task<Order?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<Order>.Filter.Eq(o => o.Id, id);
        return await _orders.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task InsertAsync(Order order, CancellationToken ct = default)
    {
        await _orders.InsertOneAsync(order, cancellationToken: ct);
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        return await _orders.Find(_ => true).ToListAsync(ct);
    }
}
```

### Accessing the raw MongoClient

When you need lower-level access (e.g. to run admin commands), `IMongoDbInstance` exposes the underlying `MongoClient`:

```csharp
public class DatabaseAdminService
{
    private readonly MongoClient _client;

    public DatabaseAdminService(IMongoDbInstance mongoDb)
    {
        _client = mongoDb.Client;
    }

    public async Task<List<string>> ListDatabaseNamesAsync(CancellationToken ct = default)
    {
        var cursor = await _client.ListDatabaseNamesAsync(ct);
        return await cursor.ToListAsync(ct);
    }
}
```

### IMongoDbInstance interface

```csharp
public interface IMongoDbInstance
{
    /// <summary>The configured MongoClient singleton.</summary>
    MongoClient Client { get; }

    /// <summary>
    /// Returns the typed collection whose name equals typeof(T).Name.
    /// </summary>
    IMongoCollection<T> GetCollectionFor<T>();
}
```

## Features

### Plugin registration

`MongoDbPlugin` implements both `IPluginServiceConfiguration` and `IPluginHealthChecksConfiguration`. It is automatically discovered by the Dosaic source generator and requires no manual registration.

The following services are registered in the DI container:

| Service | Lifetime | Description |
|---|---|---|
| `MongoDbConfiguration` | Singleton | Bound configuration object |
| `IMongoDbInstance` (`MongoDbInstance`) | Singleton | MongoDB connection and collection factory |

### Health checks

A readiness health check named `"mongo"` is registered under the `readiness` tag. It is available at the standard Dosaic health endpoint:

```
GET /health/readiness
```

The check connects to the configured host/port and verifies the named database is reachable. Authentication credentials are forwarded when present.

### OpenTelemetry tracing

The plugin subscribes the `MongoDB.Driver.Core.Extensions.DiagnosticSources` activity source so every MongoDB command appears as a span in your distributed traces:

```
ActivitySource: MongoDB.Driver.Core.Extensions.DiagnosticSources
```

No additional configuration is required — tracing is enabled automatically when the plugin is active.

### Driver metrics

`MongoDbInstance` subscribes to MongoDB driver events and emits OpenTelemetry counter metrics in snake_case format. The following counters are tracked:

| Metric name | Driver event |
|---|---|
| `mongodb_driver_connection_opened_event_total` | Connection opened |
| `mongodb_driver_connection_closed_event_total` | Connection closed |
| `mongodb_driver_connection_failed_event_total` | Connection failed |
| `mongodb_driver_connection_opening_failed_event_total` | Connection opening failed |
| `mongodb_driver_server_heartbeat_started_event_total` | Heartbeat started |
| `mongodb_driver_server_heartbeat_succeeded_event_total` | Heartbeat succeeded |
| `mongodb_driver_server_heartbeat_failed_event_total` | Heartbeat failed |
| `mongodb_driver_command_started_event_total` | Command started |
| `mongodb_driver_command_succeeded_event_total` | Command succeeded |
| `mongodb_driver_command_failed_event_total` | Command failed |
| `mongodb_driver_cluster_selecting_server_failed_event_total` | Server selection failed |
| `mongodb_driver_connection_receiving_message_failed_event_total` | Receive message failed |
| `mongodb_driver_connection_sending_messages_failed_event_total` | Send messages failed |

These counters are exposed through Dosaic's default Prometheus endpoint (`/metrics`).

### Connection settings

| Setting | Value |
|---|---|
| Connect timeout | 5 seconds |
| Heartbeat interval | 5 seconds |
| Authentication | Optional — skipped when `User` or `Password` is empty |
| Auth database | Uses `AuthDatabase` when set, otherwise falls back to `Database` |
