using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit
{
    internal class MessageSender(
        IDateTimeProvider dateTimeProvider,
        ISendEndpointProvider provider,
        IMessageValidator messageValidator,
        IMessageScheduler scheduler,
        IMessageDeduplicateKeyProvider deduplicateKeyProvider,
        IQueueResolver queueResolver,
        MessageBusConfiguration configuration) : IMessageBus

    {
        private readonly ConcurrentDictionary<Uri, ISendEndpoint> _sendEndpoints = new();
        private static readonly ConcurrentDictionary<Type, bool?> TraceLinkOverrides = new();

        // Per-message-type opt in/out via [TraceLink]; falls back to the configured default.
        private bool ShouldLink(Type messageType) =>
            TraceLinkOverrides.GetOrAdd(messageType, t => t.GetCustomAttribute<TraceLinkAttribute>()?.Enabled)
            ?? configuration.UseTraceLinks;

        private void ApplyHeaders(SendContext ctx, object message, IDictionary<string, string> headers, string traceHeader, string traceParent)
        {
            var key = deduplicateKeyProvider.TryGetKey(message);
            if (!string.IsNullOrWhiteSpace(key))
                ctx.Headers.Set(MessageBusConstants.DedupeHeader, key);
            if (headers != null && headers.Any())
            {
                foreach (var header in headers)
                {
                    ctx.Headers.Set(header.Key, header.Value);
                }
            }
            if (traceParent != null)
                ctx.Headers.Set(traceHeader, traceParent);
        }

        // We own the messaging spans end to end rather than letting MassTransit propagate its trace
        // context: a "MSG <type> send" span is created under the caller (so the parent trace shows the
        // send) and its context is stamped into a header. The ambient activity is suppressed during the
        // actual transport call so MassTransit emits no W3C traceparent. The consumer then either links
        // to that span (TraceLinkHeader -> own root trace) or continues it (TraceParentHeader).
        private async Task SendTracedAsync(Type messageType, object message, IDictionary<string, string> headers,
            Func<Action<SendContext>, Task> send)
        {
            using var sendActivity = Tracing.StartActivity(
                $"{MessageBusConstants.SpanPrefix} {messageType.DisplayName()} send", ActivityKind.Producer);
            var traceParent = sendActivity?.Id;
            var traceHeader = ShouldLink(messageType)
                ? MessageBusConstants.TraceLinkHeader
                : MessageBusConstants.TraceParentHeader;
            using var suppress = Tracing.SuppressForLinking();
            await send(ctx => ApplyHeaders(ctx, message, headers, traceHeader, traceParent));
        }

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
            await SendTracedAsync(messageType, message, headers,
                cb => endpoint.Send(message, cb, cancellationToken));
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

        public async Task ScheduleAsync(Type messageType, object message, TimeSpan duration, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            if (scheduler is null)
                throw new InvalidOperationException("Scheduler is not available and must be configured!");
            if (!messageValidator.HasConsumers(messageType)) return;
            var queue = queueResolver.ResolveSendAddress(messageType);
            await SendTracedAsync(messageType, message, headers,
                cb => scheduler.ScheduleSend(queue, duration, message, messageType, cb, cancellationToken));
        }

        public Task ScheduleAsync(Type messageType, object message, DateTime scheduledTime, IDictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            var duration = scheduledTime.Subtract(dateTimeProvider.UtcNow);
            return ScheduleAsync(messageType, message, duration, headers, cancellationToken);
        }

        private async Task<ISendEndpoint> GetSendEndpoint(Type messageType)
        {
            var address = queueResolver.ResolveSendAddress(messageType);
            if (_sendEndpoints.TryGetValue(address, out var endpoint))
                return endpoint;
            var newEndpoint = await provider.GetSendEndpoint(address);
            _sendEndpoints.TryAdd(address, newEndpoint);
            return newEndpoint;
        }
    }
}
