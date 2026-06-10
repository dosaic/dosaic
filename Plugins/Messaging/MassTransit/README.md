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
  prefetchCount: null
  useCircuitBreaker: false
  circuitBreakerTripThreshold: 10
  circuitBreakerActiveThreshold: 5
  circuitBreakerResetIntervalSeconds: 60
  useQuorumQueues: false
  quorumQueueReplicationFactor: null
  deliveryLimit: null
  useTraceLinks: true
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
| `prefetchCount` | `ushort?` | `null` | RabbitMQ prefetch count. When `null`, aligns to `[ConsumerConcurrency]` if set, otherwise uses the MassTransit default |
| `useCircuitBreaker` | `bool` | `false` | Enable circuit breaker on receive endpoints |
| `circuitBreakerTripThreshold` | `int` | `10` | Percentage of failed attempts that trips the circuit breaker (0–100) |
| `circuitBreakerActiveThreshold` | `int` | `5` | Minimum number of attempts before the circuit breaker can trip |
| `circuitBreakerResetIntervalSeconds` | `int` | `60` | Duration (seconds) the circuit stays open before resetting |
| `useQuorumQueues` | `bool` | `false` | Enable RabbitMQ quorum queues for all consumers |
| `quorumQueueReplicationFactor` | `int?` | `null` | Replication factor for quorum queues (when `null`, uses RabbitMQ default) |
| `deliveryLimit` | `int?` | `null` | Sets `x-delivery-limit` queue argument on quorum queues — controls how many times a message can be redelivered before being dead-lettered |
| `useTraceLinks` | `bool` | `true` | Default trace-linking behaviour (see [Trace Linking](#trace-linking)). Override per message type with `[TraceLink]` |

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

### Per-Consumer Concurrency Control

By default, MassTransit determines the concurrency limit. You can restrict it per consumer using `[ConsumerConcurrency]`. When multiple consumers share a queue, the **minimum** value wins:

```csharp
using Dosaic.Plugins.Messaging.MassTransit;

// This endpoint will process at most 1 message at a time
[ConsumerConcurrency(1)]
public class ImportShipmentConsumer : IMessageConsumer<EntityChange<ImportShipment>>
{
    public async Task ProcessAsync(EntityChange<ImportShipment> message, CancellationToken ct)
    {
        // Safe for scoped services like DbContext — no concurrent access
    }
}
```

When `[ConsumerConcurrency]` is set, `PrefetchCount` is automatically aligned to the concurrency limit (unless overridden in config).

### Quorum Queues

RabbitMQ [quorum queues](https://www.rabbitmq.com/docs/quorum-queues) can be enabled globally via configuration or per-consumer via the `[QuorumQueue]` attribute.

**Global** — enable for all consumers:

```yaml
messageBus:
  host: rabbitmq://localhost
  username: guest
  password: guest
  useQuorumQueues: true
  quorumQueueReplicationFactor: 3
  deliveryLimit: 5
```

**Per-consumer** — enable (or override the replication factor) for a specific consumer:

```csharp
using Dosaic.Plugins.Messaging.MassTransit;

[QuorumQueue(3)]
public class MyConsumer : MessageConsumer<MyMessage>
{
    // This consumer's queue will use quorum queues with replication factor 3
}
```

`[QuorumQueue]` without arguments enables quorum queues with the RabbitMQ default replication factor. When both global configuration and the attribute are present, the attribute takes precedence.

`DeliveryLimit` only applies to quorum queues and is ignored for classic queues.

### Per-Consumer Timeout

Add `[ConsumerTimeout]` to limit how long a consumer is allowed to process a single message:

```csharp
using Dosaic.Plugins.Messaging.MassTransit;

[ConsumerTimeout(120)] // 120 seconds
public class LongRunningConsumer : IMessageConsumer<HeavyReport>
{
    public async Task ProcessAsync(HeavyReport message, CancellationToken cancellationToken)
    {
        // cancellationToken will be cancelled after 120 seconds
    }
}
```

### Trace Linking

Out of the box MassTransit propagates the trace context across the transport (the W3C `traceparent` header), so a job that publishes N messages produces one giant trace with every downstream consumer span hanging off it — the "parent-child explosion".

This plugin owns its spans instead and **does not export MassTransit's own `ActivitySource`** (no `send`/`receive`/`process` spans). On send it creates a `MSG <type> send` (Producer) span **under the caller**, so the parent trace shows that a send happened, and stamps that span's context into a header. The consumer reads it and:

- **Linking mode** (`useTraceLinks: true`, default) — header `x-trace-link`. The consume span `MSG <Consumer>.Consume<T>` starts a **fresh root trace** and attaches the send span as an [`ActivityLink`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitylink). The two traces stay navigable via the link, without nesting.
- **Parent mode** (`useTraceLinks: false`) — header `x-trace-parent`. The consume span **continues the send span's trace** as a child (one trace end to end).

```
linking mode:
  caller trace:   caller → MSG ImportShipment send      (link ⇢)
  receive trace:  MSG …ImportShipmentConsumer.Consume<>  (root, ⇠ linked)
                    └ ProcessAsync
```

> **Trade-off:** MassTransit's built-in transport spans (`receive`/`process`/retry timings) are no longer exported. The `Dosaic` metrics (`messaging.consumer.*`) are unaffected.

Toggle the default globally with `useTraceLinks`, or override per message type with `[TraceLink]` (the attribute takes precedence over configuration):

```csharp
using Dosaic.Plugins.Messaging.MassTransit;

[TraceLink(false)] // parent mode: consume span continues the send's trace
public record AuditLogged(Guid Id) : IMessage;

[TraceLink] // force linking even when useTraceLinks is false
public record OrderPlaced(Guid OrderId) : IMessage;
```

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

    // Called for each receive endpoint (queue) — receives the consumer types on this endpoint
    public void ConfigureReceiveEndpoint(IBusRegistrationContext context, Uri queue, Type[] consumerTypes,
        IRabbitMqReceiveEndpointConfigurator configurator)
    {
        // Example: set prefetch based on consumer types
        if (consumerTypes.Any(t => t.Name.Contains("Import")))
            configurator.PrefetchCount = 1;
    }
}
```

> **Backward compatibility:** The 3-parameter `ConfigureReceiveEndpoint(context, queue, configurator)` overload still works via a default interface method. Existing implementations do not need to change.

## Features

- **Auto-discovery** — all `IMessageConsumer<T>` implementations in the application are found at startup without explicit registration.
- **Multiple consumers per queue** — any number of consumers may handle the same message type; they are run concurrently, and all exceptions are collected and re-thrown as an `AggregateException`.
- **Scoped retry with fresh DI context** — each retry gets a fresh DI scope via `UseMessageScope` + `UseInMemoryOutbox`. This ensures scoped services like `DbContext` are recreated on every attempt, preventing thread-safety issues.
- **Built-in retry** — configurable immediate retry (`useRetry`, `maxRetryCount`, `retryDelaySeconds`) and delayed redelivery (`maxRedeliveryCount`, `redeliveryDelaySeconds`) using MassTransit middleware.
- **Per-consumer concurrency control** — `[ConsumerConcurrency(n)]` attribute to limit concurrent message processing per endpoint. When multiple consumers share a queue, the minimum value wins. `PrefetchCount` auto-aligns to the concurrency limit.
- **Per-consumer timeout** — `[ConsumerTimeout(seconds)]` attribute to set a processing deadline per consumer. The `CancellationToken` passed to `ProcessAsync` will be cancelled after the configured duration.
- **Quorum queues** — enable RabbitMQ quorum queues globally (`useQuorumQueues: true`) or per-consumer via `[QuorumQueue]` attribute, with optional replication factor and delivery limit. The attribute takes precedence over global configuration.
- **Circuit breaker** — configurable circuit breaker (`useCircuitBreaker`) to stop processing when failure thresholds are exceeded, preventing cascading failures.
- **Scheduled sending** — send messages at a future point in time using `ScheduleAsync` with a `TimeSpan` or `DateTime`.
- **Custom message headers** — pass arbitrary headers via `IDictionary<string, string>` overloads of `SendAsync` / `ScheduleAsync`.
- **Message deduplication** — SHA-256 content hash (or custom factory) written to `x-deduplication-header`.
- **Queue name customisation** — automatic resolution from type name; `[QueueName("...")]` attribute for explicit overrides; generic types produce hyphen-joined segment names.
- **In-memory transport** — set `useInMemory: true` for integration tests without a running RabbitMQ broker.
- **Observability** — `messaging.consumer.duration` (histogram) and `messaging.consumer.failures` (counter) metrics emitted per consumer invocation with `consumer_type` and `message_type` tags. Spans are emitted on the shared `Dosaic` activity source; MassTransit's own source is intentionally not exported (see [Trace Linking](#trace-linking)).
- **Trace linking** — a `MSG <type> send` span is created under the caller (parent trace) and the consumer either starts its own linked root trace (`useTraceLinks: true`) or continues it as a child (`false`). Toggle globally with `useTraceLinks` or per message type with `[TraceLink]` (attribute wins over configuration).
- **Health checks** — a MassTransit health check named `message-bus` is registered under the `readiness` tag; reports `Unhealthy` on failure.
- **Extensible** — `IMessageBusConfigurator` allows full access to the MassTransit configuration API for advanced scenarios. The `ConfigureReceiveEndpoint` overload now provides the consumer types registered on each endpoint.


