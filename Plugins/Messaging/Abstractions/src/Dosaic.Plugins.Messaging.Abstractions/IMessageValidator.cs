namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageValidator
{
    bool HasConsumers<TMessage>() where TMessage : IMessage;
}
