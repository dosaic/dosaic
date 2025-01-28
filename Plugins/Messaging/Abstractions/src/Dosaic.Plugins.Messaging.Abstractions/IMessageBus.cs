namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageBus
{
    Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IMessage;
}
