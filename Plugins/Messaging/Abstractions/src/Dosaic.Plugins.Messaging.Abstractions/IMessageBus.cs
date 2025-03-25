namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageBus
{
    Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    Task SendAsync(Type messageType, object message, CancellationToken cancellationToken = default);
    Task ScheduleAsync<TMessage>(TMessage message, TimeSpan duration, CancellationToken cancellationToken = default) where TMessage : IMessage;
    Task ScheduleAsync<TMessage>(TMessage message, DateTime scheduledDate, CancellationToken cancellationToken = default) where TMessage : IMessage;

    Task ScheduleAsync(Type messageType, object message, TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime,
        CancellationToken cancellationToken = default);
}
