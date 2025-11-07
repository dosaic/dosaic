using System.Collections.Concurrent;
using Chronos.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit
{
    internal class MessageSender(
        IDateTimeProvider dateTimeProvider,
        ISendEndpointProvider provider,
        IMessageValidator messageValidator,
        IMessageScheduler scheduler,
        IMessageDeduplicateKeyProvider deduplicateKeyProvider) : IMessageBus
    {
        private readonly ConcurrentDictionary<Uri, ISendEndpoint> _sendEndpoints = new();
        private const string DedupeHeader = "x-deduplication-header";

        public Task SendAsync<TMessage>(TMessage message, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default)
            where TMessage : IMessage
        {
            return SendAsync(typeof(TMessage), message, headers, cancellationToken);
        }

        public async Task SendAsync(Type messageType, object message, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            if (!messageValidator.HasConsumers(messageType)) return;
            var endpoint = await GetSendEndpoint(messageType);
            await endpoint.Send(message, ctx =>
            {
                ctx.Durable = true;
                var key = deduplicateKeyProvider.TryGetKey(message);
                if (!string.IsNullOrWhiteSpace(key))
                    ctx.Headers.Set(DedupeHeader, key);
                if (headers != null && headers.Any())
                {
                    foreach (var header in headers)
                    {
                        ctx.Headers.Set(header.Key, header.Value);
                    }
                }
            }, cancellationToken);
        }

        public Task ScheduleAsync<TMessage>(TMessage message, TimeSpan duration, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default) where TMessage : IMessage
        {
            return ScheduleAsync(typeof(TMessage), message, duration, headers, cancellationToken);
        }

        public Task ScheduleAsync<TMessage>(TMessage message, DateTime scheduledDate, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default) where TMessage : IMessage
        {
            return ScheduleAsync(typeof(TMessage), message, scheduledDate, headers, cancellationToken);
        }

        public Task ScheduleAsync(Type messageType, object message, TimeSpan duration, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            var dateToSend = dateTimeProvider.UtcNow.Add(duration);
            return ScheduleAsync(messageType, message, dateToSend, headers, cancellationToken);
        }

        public async Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            if (scheduler is null)
                throw new InvalidOperationException("Scheduler is not available and must be configured!");
            if (!messageValidator.HasConsumers(messageType)) return;
            var queue = QueueResolver.Resolve(messageType);
            await scheduler.ScheduleSend<object>(
                queue,
                scheduledTime,
                message,
                Pipe.Execute<SendContext<object>>(ctx =>
                {
                    ctx.Durable = true;
                    var key = deduplicateKeyProvider.TryGetKey(ctx.Message);
                    if (!string.IsNullOrWhiteSpace(key))
                        ctx.Headers.Set(DedupeHeader, key);

                    if (headers != null && headers.Any())
                    {
                        foreach (var header in headers)
                        {
                            ctx.Headers.Set(header.Key, header.Value);
                        }
                    }
                }),
                cancellationToken);
        }

        private async Task<ISendEndpoint> GetSendEndpoint(Type messageType)
        {
            var address = QueueResolver.Resolve(messageType);
            if (_sendEndpoints.TryGetValue(address, out var endpoint))
                return endpoint;
            var newEndpoint = await provider.GetSendEndpoint(address);
            _sendEndpoints.TryAdd(address, newEndpoint);
            return newEndpoint;
        }
    }
}
