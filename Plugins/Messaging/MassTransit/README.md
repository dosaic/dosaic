# Dosaic.Plugins.Messaging.MassTransit

`Dosaic.Plugins.Messaging.MassTransit` is a plugin that provides message-bus capabilities for Dosaic applications using [MassTransit](https://masstransit.io/) over **RabbitMQ** (or an in-memory transport for integration tests). It auto-discovers all `IMessageConsumer<T>` implementations at startup, wires them to their queues, and exposes a simple `IMessageBus` abstraction for sending and scheduling messages.

## Installation

```shell
dotnet add package Dosaic.Plugins.Messaging.MassTransit
```

Or add a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Messaging.MassTransit" Version="" />
```

## Configuration

The plugin is activated automatically by the Dosaic plugin system. Configure it in your `appsettings.yml` (or `appsettings.json`) under the `messageBus` key:

```yaml
messageBus:
  host: localhost
  port: 5672
  vhost: /
  username: rabbitmq
  password: rabbitmq
  useRetry: false
  maxRetryCount: 3
  retryDelaySeconds: 30
  maxRedeliveryCount: 3
  redeliveryDelaySeconds: 30
  deduplication: false
  useInMemory: false
```

| Property | Type | Default | Description |
|---|---|---|---|
| `host` | `string` | `""` | RabbitMQ hostname |
| `port` | `ushort` | `5672` | RabbitMQ AMQP port |
| `vhost` | `string` | `"/"` | RabbitMQ virtual host |
| `username` | `string` | — | RabbitMQ username (optional) |
| `password` | `string` | — | RabbitMQ password (optional) |
| `useRetry` | `bool` | `false` | Enable immediate message retry on consumer failure |
| `maxRetryCount` | `int` | `3` | Number of immediate retry attempts |
| `retryDelaySeconds` | `int` | `30` | Delay between immediate retries (seconds) |
| `maxRedeliveryCount` | `int` | `3` | Number of delayed redelivery attempts |
| `redeliveryDelaySeconds` | `int` | `30` | Delay between redelivery attempts (seconds) |
| `deduplication` | `bool` | `false` | Enable message deduplication via `x-deduplication-header` |
| `useInMemory` | `bool` | `false` | Use in-memory transport instead of RabbitMQ (useful for testing) |

> **Note:** When `useInMemory` is `true`, all RabbitMQ settings are ignored.

## Usage

### Defining Messages

All messages must implement `IMessage` from `Dosaic.Plugins.Messaging.Abstractions`:

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

// Simple message
public record OrderPlaced(Guid OrderId, decimal Total) : IMessage;

// Generic message — queue name becomes "Notification-String"
public record Notification<T>(T Payload) : IMessage;
```

### Queue Name Resolution

Queue names are resolved automatically from the message type name. You can override this with `[QueueName]`:

```csharp
using Dosaic.Plugins.Messaging.Abstractions;
using Dosaic.Plugins.Messaging.MassTransit;

// Queue name: "OrderPlaced" (from type name)
public record OrderPlaced(Guid OrderId) : IMessage;

// Queue name: "order-placed-v2" (from attribute)
[QueueName("order-placed-v2")]
public record OrderPlacedV2(Guid OrderId) : IMessage;

// Queue name: "Notification-String" (generic type segments joined with "-")
public record Notification<T>(T Payload) : IMessage;
```

### Implementing Consumers

Implement `IMessageConsumer<TMessage>` for each message type you want to handle. Multiple consumers for the same message type are supported — they are all invoked concurrently:

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

public class OrderPlacedConsumer : IMessageConsumer<OrderPlaced>
{
    public async Task ProcessAsync(OrderPlaced message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order {message.OrderId} placed for {message.Total}");
    }
}

// Second consumer on the same queue — both are called for every message
public class OrderPlacedAuditConsumer : IMessageConsumer<OrderPlaced>
{
    public async Task ProcessAsync(OrderPlaced message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Audit: order {message.OrderId}");
    }
}
```

Consumers are auto-discovered via the Dosaic source generator and registered automatically — no manual registration is required.

### Sending Messages

Inject `IMessageBus` and call `SendAsync`:

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

public class OrderService(IMessageBus bus)
{
    public async Task PlaceOrderAsync(Guid orderId, decimal total, CancellationToken ct)
    {
        await bus.SendAsync(new OrderPlaced(orderId, total), cancellationToken: ct);
    }
}
```

You can also send with custom headers:

```csharp
var headers = new Dictionary<string, string>
{
    ["x-correlation-id"] = correlationId,
    ["x-tenant-id"] = tenantId
};
await bus.SendAsync(new OrderPlaced(orderId, total), headers, ct);
```

If no consumer is registered for the message type, `SendAsync` is a no-op (no exception is thrown).

### Scheduling Messages

Schedule a message to be delivered after a delay or at an absolute time. Scheduling requires `IMessageScheduler` to be available in the container (provided by MassTransit's delayed-message scheduler):

```csharp
// Deliver in 10 minutes
await bus.ScheduleAsync(new OrderPlaced(orderId, total), TimeSpan.FromMinutes(10), cancellationToken: ct);

// Deliver at a specific UTC time
await bus.ScheduleAsync(new OrderPlaced(orderId, total), DateTime.UtcNow.AddHours(2), cancellationToken: ct);
```

### Message Deduplication

Enable deduplication in configuration (`deduplication: true`). By default, deduplicate keys are derived from a SHA-256 hash of the JSON-serialised message body prefixed with the full type name.

You can register a custom key factory per message type via `IMessageDeduplicateKeyProvider`:

```csharp
using Dosaic.Plugins.Messaging.MassTransit;

public class MyStartup(IMessageDeduplicateKeyProvider dedup)
{
    public void Configure()
    {
        // Use a stable business key instead of JSON hash
        dedup.Register<OrderPlaced>(msg => $"order:{msg.OrderId}");
    }
}
```

The deduplication key is written to the `x-deduplication-header` AMQP header. You must pair this with a RabbitMQ deduplication plugin on the broker side.

### Advanced Bus Configuration via `IMessageBusConfigurator`

Implement `IMessageBusConfigurator` to hook into the MassTransit configuration pipeline. Any number of configurators are discovered automatically and applied in order:

```csharp
using Dosaic.Plugins.Messaging.MassTransit;
using MassTransit;

public class MyBusConfigurator : IMessageBusConfigurator
{
    // Called once during IBusRegistrationConfigurator setup
    public void ConfigureMassTransit(IBusRegistrationConfigurator opts)
    {
        opts.AddRequestClient<OrderPlaced>();
    }

    // Called for the RabbitMQ bus factory (not invoked when useInMemory = true)
    public void ConfigureRabbitMq(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator config)
    {
        config.UseMessageData(/* ... */);
    }

    // Called for each receive endpoint (queue)
    public void ConfigureReceiveEndpoint(IBusRegistrationContext context, Uri queue,
        IRabbitMqReceiveEndpointConfigurator configurator)
    {
        configurator.PrefetchCount = 10;
    }
}
```

## Features

- **Auto-discovery** — all `IMessageConsumer<T>` implementations in the application are found at startup without explicit registration.
- **Multiple consumers per queue** — any number of consumers may handle the same message type; they are run concurrently, and all exceptions are collected and re-thrown as an `AggregateException`.
- **Built-in retry** — configurable immediate retry (`useRetry`, `maxRetryCount`, `retryDelaySeconds`) and delayed redelivery (`maxRedeliveryCount`, `redeliveryDelaySeconds`) using MassTransit middleware.
- **Per-consumer Polly retry** — each consumer call is wrapped in a `WaitAndRetryAsync` Polly policy with up to 2 exponential back-off attempts before the error is propagated to MassTransit's error queue (`QUEUE_NAME_error`).
- **Scheduled sending** — send messages at a future point in time using `ScheduleAsync` with a `TimeSpan` or `DateTime`.
- **Custom message headers** — pass arbitrary headers via `IDictionary<string, string>` overloads of `SendAsync` / `ScheduleAsync`.
- **Message deduplication** — SHA-256 content hash (or custom factory) written to `x-deduplication-header`.
- **Queue name customisation** — automatic resolution from type name; `[QueueName("...")]` attribute for explicit overrides; generic types produce hyphen-joined segment names.
- **In-memory transport** — set `useInMemory: true` for integration tests without a running RabbitMQ broker.
- **OpenTelemetry tracing** — MassTransit activity source is registered automatically with the OpenTelemetry `TracerProvider`.
- **Health checks** — a MassTransit health check named `message-bus` is registered under the `readiness` tag; reports `Unhealthy` on failure.
- **Extensible** — `IMessageBusConfigurator` allows full access to the MassTransit configuration API for advanced scenarios.


