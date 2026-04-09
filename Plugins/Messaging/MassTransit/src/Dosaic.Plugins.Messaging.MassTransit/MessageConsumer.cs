using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Dosaic.Hosting.Abstractions.Metrics;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageConsumer<TMessage>(ILogger<MessageConsumer<TMessage>> logger, IEnumerable<IMessageConsumer<TMessage>> consumers) : IConsumer<TMessage>
    where TMessage : class, IMessage
{
    private static readonly string MessageTypeName = typeof(TMessage).Name;

    private static readonly ConcurrentDictionary<Type, int?> TimeoutCache = new();

    private static readonly Counter<long> FailureCounter =
        Metrics.CreateCounter<long>("messaging.consumer.failures", "failures", "Number of consumer processing failures");

    private static readonly Histogram<double> DurationHistogram =
        Metrics.CreateHistogram<double>("messaging.consumer.duration", "ms", "Consumer processing duration");

    private static int? GetTimeoutSeconds(Type consumerType) =>
        TimeoutCache.GetOrAdd(consumerType, t => t.GetCustomAttribute<ConsumerTimeoutAttribute>()?.TimeoutSeconds);

    public async Task Consume(ConsumeContext<TMessage> context)
    {
        var consumerTasks = consumers.Select(x => ProcessAsync(x, context.Message, context.CancellationToken));
        var exceptions = (await Task.WhenAll(consumerTasks))
            .Where(x => x != null)
            .Select(x => x!)
            .ToArray();
        if (exceptions.Length > 0)
            throw new AggregateException(exceptions);
    }

    private async Task<Exception> ProcessAsync(IMessageConsumer<TMessage> consumer, TMessage message, CancellationToken cancellationToken)
    {
        var consumerTypeName = consumer.GetType().Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var timeoutSeconds = GetTimeoutSeconds(consumer.GetType());

            if (timeoutSeconds.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
                await consumer.ProcessAsync(message, cts.Token);
            }
            else
            {
                await consumer.ProcessAsync(message, cancellationToken);
            }

            return null;
        }
        catch (Exception e)
        {
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
        }
    }
}
