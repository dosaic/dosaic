# Dosaic.Plugins.Messaging.Abstractions

Core abstractions for Dosaic messaging plugins. This package defines the contracts that messaging implementations (such as `Dosaic.Plugins.Messaging.MassTransit`) must fulfill, and that application code depends on to send, schedule, and consume messages in a transport-agnostic way.

## Installation

```shell
dotnet add package Dosaic.Plugins.Messaging.Abstractions
```

## Interfaces

### `IMessage`

Marker interface that every message contract must implement. It carries no members — its only purpose is to allow the framework and transport implementations to identify and route message types safely.

```csharp
public interface IMessage;
```

---

### `IMessageBus`

The primary entry point for publishing messages. Inject `IMessageBus` wherever you need to send or schedule messages. The concrete implementation is registered by the chosen messaging plugin (e.g. `MessageBusPlugin` from `Dosaic.Plugins.Messaging.MassTransit`).

```csharp
public interface IMessageBus
{
    // Send a strongly-typed message immediately
    Task SendAsync<TMessage>(TMessage message,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    // Send a message when the type is only known at runtime
    Task SendAsync(Type messageType, object message,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);

    // Schedule a strongly-typed message after a relative delay
    Task ScheduleAsync<TMessage>(TMessage message, TimeSpan duration,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    // Schedule a strongly-typed message at an absolute point in time
    Task ScheduleAsync<TMessage>(TMessage message, DateTime scheduledDate,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    // Schedule an untyped message after a relative delay
    Task ScheduleAsync(Type messageType, object message, TimeSpan duration,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);

    // Schedule an untyped message at an absolute point in time
    Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime,
        IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);
}
```

**Notes:**

- `SendAsync` and `ScheduleAsync` are no-ops when no consumer is registered for the given message type — messages are silently dropped rather than raising an error.
- `ScheduleAsync` requires a configured message scheduler. If the underlying transport has no scheduler configured, an `InvalidOperationException` is thrown.
- Optional `headers` are forwarded as transport-level headers on every send/schedule call.

---

### `IMessageConsumer<TMessage>`

Implement this interface to handle incoming messages of a specific type. Multiple consumers for the same message type are supported — all registered consumers are invoked in parallel when a message arrives.

```csharp
public interface IMessageConsumer<in TMessage>
    where TMessage : IMessage
{
    Task ProcessAsync(TMessage message, CancellationToken cancellationToken = default);
}
```

The messaging plugin discovers all `IMessageConsumer<TMessage>` implementations at startup and registers them automatically. No additional registration is required.

---

### `IMessageValidator`

Internal contract used by messaging implementations to check whether a registered consumer exists for a given message type before attempting to route a message. Application code does not normally need to interact with this interface directly.

```csharp
public interface IMessageValidator
{
    bool HasConsumers(Type t);
}
```

## Usage

### Defining a message

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

namespace MyService.Messages;

public record OrderPlaced(Guid OrderId, decimal Total) : IMessage;
```

### Implementing a consumer

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

namespace MyService.Consumers;

public class OrderPlacedConsumer : IMessageConsumer<OrderPlaced>
{
    public async Task ProcessAsync(OrderPlaced message, CancellationToken cancellationToken = default)
    {
        // Handle the message
        Console.WriteLine($"Order {message.OrderId} placed for {message.Total:C}");
    }
}
```

The consumer is automatically discovered and registered by the messaging plugin — no explicit `services.Add*` call is needed.

### Sending a message

Inject `IMessageBus` and call `SendAsync`:

```csharp
using Dosaic.Plugins.Messaging.Abstractions;

namespace MyService.Services;

public class OrderService(IMessageBus messageBus)
{
    public async Task PlaceOrder(Guid orderId, decimal total, CancellationToken ct)
    {
        // ... business logic ...

        await messageBus.SendAsync(new OrderPlaced(orderId, total), cancellationToken: ct);
    }
}
```

### Scheduling a message

Use `ScheduleAsync` with either a `TimeSpan` (relative) or a `DateTime` (absolute):

```csharp
// Send after 30 minutes
await messageBus.ScheduleAsync(new OrderPlaced(orderId, total), TimeSpan.FromMinutes(30), cancellationToken: ct);

// Send at a specific UTC time
await messageBus.ScheduleAsync(new OrderPlaced(orderId, total), DateTime.UtcNow.AddHours(2), cancellationToken: ct);
```

### Sending with custom headers

```csharp
var headers = new Dictionary<string, string>
{
    ["x-correlation-id"] = correlationId.ToString(),
    ["x-tenant-id"] = tenantId
};

await messageBus.SendAsync(new OrderPlaced(orderId, total), headers, ct);
```

