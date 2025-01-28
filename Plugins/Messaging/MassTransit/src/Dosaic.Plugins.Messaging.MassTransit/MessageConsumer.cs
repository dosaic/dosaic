using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Polly;

namespace Dosaic.Plugins.Messaging.MassTransit;

internal class MessageConsumer<TMessage>(ILogger<MessageConsumer<TMessage>> logger, IEnumerable<IMessageConsumer<TMessage>> consumers) : IConsumer<TMessage>
    where TMessage : class, IMessage
{
    private const int MaxRetryAttempts = 2;
    private readonly IAsyncPolicy _retryPolicy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(MaxRetryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
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
        try
        {
            await _retryPolicy.ExecuteAsync(async ct => await consumer.ProcessAsync(message, ct), cancellationToken);
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not process message with consumer {ConsumerType} after {MaxRetryAttempts} retries", consumer.GetType().Name, MaxRetryAttempts);
            return e;
        }
    }
}
