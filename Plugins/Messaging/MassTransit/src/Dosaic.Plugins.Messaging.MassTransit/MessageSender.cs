using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageSender(ISendEndpointProvider provider, IMessageValidator messageValidator) : IMessageBus
{
    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IMessage
    {
        return SendAsync(typeof(TMessage), message, cancellationToken);
    }

    public async Task SendAsync(Type messageType, object message, CancellationToken cancellationToken = default)
    {
        if (!messageValidator.HasConsumers(messageType)) return;
        var endpoint = await provider.GetSendEndpoint(QueueResolver.Resolve(messageType));
        await endpoint.Send(message, cancellationToken);
    }
}
