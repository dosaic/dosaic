using System.Collections.Concurrent;
using Chronos.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageSender(IDateTimeProvider dateTimeProvider, ISendEndpointProvider provider, IMessageValidator messageValidator, IMessageScheduler scheduler) : IMessageBus
{
    private readonly ConcurrentDictionary<Uri, ISendEndpoint> _sendEndpoints = new();

    private async Task<ISendEndpoint> GetSendEndpoint(Type messageType)
    {
        var address = QueueResolver.Resolve(messageType);
        if (_sendEndpoints.TryGetValue(address, out var endpoint))
            return endpoint;
        var newEndpoint = await provider.GetSendEndpoint(address);
        _sendEndpoints.TryAdd(address, newEndpoint);
        return newEndpoint;
    }

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IMessage
    {
        return SendAsync(typeof(TMessage), message, cancellationToken);
    }

    public async Task SendAsync(Type messageType, object message, CancellationToken cancellationToken = default)
    {
        if (!messageValidator.HasConsumers(messageType)) return;
        var endpoint = await GetSendEndpoint(messageType);
        await endpoint.Send(message, cancellationToken);
    }

    public Task ScheduleAsync<TMessage>(TMessage message, TimeSpan duration, CancellationToken cancellationToken = default) where TMessage : IMessage
    {
        return ScheduleAsync(typeof(TMessage), message, duration, cancellationToken);
    }

    public Task ScheduleAsync<TMessage>(TMessage message, DateTime scheduledDate, CancellationToken cancellationToken = default) where TMessage : IMessage
    {
        return ScheduleAsync(typeof(TMessage), message, scheduledDate, cancellationToken);
    }

    public Task ScheduleAsync(Type messageType, object message, TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var dateToSend = dateTimeProvider.UtcNow.Add(duration);
        return ScheduleAsync(messageType, message, dateToSend, cancellationToken);
    }

    public async Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime,
        CancellationToken cancellationToken = default)
    {
        if (scheduler is null) throw new InvalidOperationException("Scheduler is not available and must be configured!");
        if (!messageValidator.HasConsumers(messageType)) return;
        var queue = QueueResolver.Resolve(messageType);
        await scheduler.ScheduleSend(queue, scheduledTime, message, cancellationToken);
    }
}
