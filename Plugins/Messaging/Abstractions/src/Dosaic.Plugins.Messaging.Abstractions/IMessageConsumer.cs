namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageConsumer<in TMessage>
    where TMessage : IMessage
{
    Task ProcessAsync(TMessage message, CancellationToken cancellationToken = default);
}
