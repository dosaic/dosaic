# Dosaic.Plugins.Messaging.MassTransit

Dosaic.Plugins.Messaging.MassTransit is a `plugin` that allows other `Dosaic components` to `use messaging using MassTransit`.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Messaging.MassTransit
```
or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Messaging.MassTransit" Version="" />
```

## Appsettings.yml

Configure your appsettings.yml with these properties

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
```

## Usage

This plugin automatically detects all queues by their implementation (IMessageConsumer<T>) and starts listening to them.
The queue name is resolved using the message type name. If you want to override this behavior, you can use the QueueNameAttribute.
You can have multiple consumers for the same message type, because it is wrapped into one consumer.
It will throw exceptions and uses the MassTransit logic for error handling, if a consumer fails.
So you will get a "QUEUE_NAME_error"-queue with the exception details and the original message.

```csharp

Example:
```csharp
internal record TestMessage : IMessage;

internal class TestService(IMessageBus bus)
{
    public Task DoStuff()
    {
        return bus.SendAsync(new TestMessage());
    }
}

internal class TestConsumer : IMessageConsumer<TestMessage>
{
    public async Task ProcessAsync(TestMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine("Received message");
    }
}
```


