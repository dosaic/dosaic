using MassTransit;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ
{
    internal class ScheduledMessageSendFunction
    {
        private readonly ISendEndpointProvider _sendEndpointProvider;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ScheduledMessageSendFunction> _logger;

        public ScheduledMessageSendFunction(ISendEndpointProvider sendEndpointProvider,
            IPublishEndpoint publishEndpoint,
            ILogger<ScheduledMessageSendFunction> logger)
        {
            _sendEndpointProvider = sendEndpointProvider;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        [TickerFunction("masstransit-scheduled-send")]
        public async Task SendScheduledMessage(TickerFunctionContext<ScheduledMessagePayload> context,
            CancellationToken cancellationToken)
        {
            var payload = context.Request;
            var (messageType, message) = payload.Deserialize();

            if (payload.Mode == ScheduledMessageMode.Publish)
            {
                _logger.LogInformation("Executing scheduled publish for {MessageType}", messageType.Name);
                await _publishEndpoint.Publish(message, messageType, ctx =>
                {
                    ApplyHeaders(ctx, payload);
                }, cancellationToken);
            }
            else
            {
                var destination = new Uri(payload.DestinationAddress);
                _logger.LogInformation("Executing scheduled send for {MessageType} to {Destination}",
                    messageType.Name, destination);

                var endpoint = await _sendEndpointProvider.GetSendEndpoint(destination);
                await endpoint.Send(message, messageType, ctx =>
                {
                    ApplyHeaders(ctx, payload);
                }, cancellationToken);
            }
        }

        private static void ApplyHeaders(SendContext ctx, ScheduledMessagePayload payload)
        {
            if (payload.Headers == null) return;
            foreach (var header in payload.Headers)
                ctx.Headers.Set(header.Key, header.Value);
        }
    }
}
