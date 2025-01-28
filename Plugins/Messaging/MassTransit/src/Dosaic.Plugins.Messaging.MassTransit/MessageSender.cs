using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageSender(ISendEndpointProvider provider, IMessageValidator messageValidator) : IMessageBus
{
    public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IMessage
    {
        if (!messageValidator.HasConsumers<TMessage>()) return;
        var endpoint = await provider.GetSendEndpoint(QueueResolver.Resolve<TMessage>());
        await endpoint.Send(message, cancellationToken);
    }
}
