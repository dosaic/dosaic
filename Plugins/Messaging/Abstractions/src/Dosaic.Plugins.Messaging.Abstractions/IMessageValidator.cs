namespace Dosaic.Plugins.Messaging.Abstractions;

public interface IMessageValidator
{
    bool HasConsumers(Type t);
}
