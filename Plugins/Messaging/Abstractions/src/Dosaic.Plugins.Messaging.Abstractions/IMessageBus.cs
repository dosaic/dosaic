namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageBus
{
    Task SendAsync<TMessage>(TMessage message, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    Task SendAsync(Type messageType, object message, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default);
    Task ScheduleAsync<TMessage>(TMessage message, TimeSpan duration, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) where TMessage : IMessage;
    Task ScheduleAsync<TMessage>(TMessage message, DateTime scheduledDate, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default) where TMessage : IMessage;

    Task ScheduleAsync(Type messageType, object message, TimeSpan duration, IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);

    Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime, IDictionary<string, string> headers = null,
        CancellationToken cancellationToken = default);
}
