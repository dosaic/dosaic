using Dosaic.Plugins.Messaging.Abstractions;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageValidator(Type[] consumedMessageTypes) : IMessageValidator
{
    public bool HasConsumers(Type t) => consumedMessageTypes.Contains(t);
}
