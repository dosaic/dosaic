using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Metrics;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageConsumer<TMessage>(ILogger<MessageConsumer<TMessage>> logger, IEnumerable<IMessageConsumer<TMessage>> consumers) : IConsumer<TMessage>
    where TMessage : class, IMessage
{
    private static readonly string MessageTypeName = typeof(TMessage).DisplayName();

    private static readonly ConcurrentDictionary<Type, int?> TimeoutCache = new();

    private static readonly Counter<long> FailureCounter =
        Metrics.CreateCounter<long>("messaging.consumer.failures", "failures", "Number of consumer processing failures");

    private static readonly Histogram<double> DurationHistogram =
        Metrics.CreateHistogram<double>("messaging.consumer.duration", "ms", "Consumer processing duration");

    private static int? GetTimeoutSeconds(Type consumerType) =>
        TimeoutCache.GetOrAdd(consumerType, t => t.GetCustomAttribute<ConsumerTimeoutAttribute>()?.TimeoutSeconds);

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var trace = ReadTraceContext(context);
        var consumerTasks = consumers.Select(x => ProcessAsync(x, context.Message, trace, context.CancellationToken));
        var exceptions = (await Task.WhenAll(consumerTasks))
            .Where(x => x != null)
            .Select(x => x!)
            .ToArray();
        if (exceptions.Length > 0)
            throw new AggregateException(exceptions);
    }

    // The sender stamps its span into one of two headers (see MessageBusConstants). TraceLinkHeader ->
    // start a fresh root trace and attach the sender as an ActivityLink; TraceParentHeader -> continue
    // the sender's trace as a child. Neither -> ambient behaviour.
    private static (ActivityContext Parent, ActivityLink[] Links) ReadTraceContext(ConsumeContext<TMessage> context)
    {
        if (TryParseHeader(context, MessageBusConstants.TraceLinkHeader, out var linkCtx))
            return (default, [new ActivityLink(linkCtx)]);
        if (TryParseHeader(context, MessageBusConstants.TraceParentHeader, out var parentCtx))
            return (parentCtx, null);
        return (default, null);
    }

    private static bool TryParseHeader(ConsumeContext<TMessage> context, string name, out ActivityContext context_)
    {
        context_ = default;
        object value = null;
        return context.Headers?.TryGetHeader(name, out value) == true
               && value is string s
               && ActivityContext.TryParse(s, null, true, out context_);
    }

    // Starts the consume span. ActivitySource ignores an explicitly-passed default ActivityContext
    // when Activity.Current is set (it inherits the ambient activity as parent instead), so to start
    // a fresh root trace in linking mode the ambient activity MUST be cleared first.
    private static Activity StartConsumeActivity(string spanName, (ActivityContext Parent, ActivityLink[] Links) trace)
    {
        Activity.Current = null;
        if (trace.Parent != default)
            return Tracing.StartActivity(spanName, ActivityKind.Consumer, trace.Parent);
        if (trace.Links != null)
            return Tracing.StartActivity(spanName, ActivityKind.Consumer, default, links: trace.Links);
        return Tracing.StartActivity(spanName, ActivityKind.Consumer);
    }

    private async Task<Exception> ProcessAsync(IMessageConsumer<TMessage> consumer, TMessage message,
        (ActivityContext Parent, ActivityLink[] Links) trace, CancellationToken cancellationToken)
    {
        var consumerType = consumer.GetType();
        var consumerTypeName = consumerType.Name;
        var spanName = $"{MessageBusConstants.SpanPrefix} {MessageTypeName} consume";
        var previous = Activity.Current;
        var activity = StartConsumeActivity(spanName, trace);
        activity?.SetTag("messaging.consumer_type", consumerType.DisplayName(fullName: true));
        activity?.SetTag("messaging.message_type", MessageTypeName);
        var sw = Stopwatch.StartNew();
        try
        {
            var timeoutSeconds = GetTimeoutSeconds(consumerType);

            if (timeoutSeconds.HasValue)
            {
                activity?.SetTag("messaging.timeout_seconds", timeoutSeconds.Value);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
                await consumer.ProcessAsync(message, cts.Token);
            }
            else
            {
                await consumer.ProcessAsync(message, cancellationToken);
            }

            activity?.SetOkStatus();
            return null;
        }
        catch (Exception e)
        {
            activity?.SetErrorStatus(e);
            FailureCounter.Add(1,
                new KeyValuePair<string, object>("consumer_type", consumerTypeName),
                new KeyValuePair<string, object>("message_type", MessageTypeName),
                new KeyValuePair<string, object>("exception_type", e.GetType().Name));
            logger.LogError(e,
                "Could not process message with consumer {ConsumerType} for message type {MessageType}",
                consumerTypeName, MessageTypeName);
            return e;
        }
        finally
        {
            sw.Stop();
            DurationHistogram.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object>("consumer_type", consumerTypeName),
                new KeyValuePair<string, object>("message_type", MessageTypeName));
            activity?.Dispose();
            Activity.Current = previous;
        }
    }
}
