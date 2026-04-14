using MassTransit;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ
{
    internal class TickerQMessageScheduler : IMessageScheduler
    {
        private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
        private readonly ILogger<TickerQMessageScheduler> _logger;
        private readonly string _functionName;

        public TickerQMessageScheduler(
            ITimeTickerManager<TimeTickerEntity> timeTickerManager,
            TickerQMessageSchedulerConfiguration configuration,
            ILogger<TickerQMessageScheduler> logger)
        {
            _timeTickerManager = timeTickerManager;
            _logger = logger;
            _functionName = configuration.FunctionName;
        }

        // --- ScheduleSend (generic, typed message) ---

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            T message, CancellationToken cancellationToken) where T : class
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(typeof(T), message, ScheduledMessageMode.Send, destinationAddress),
                cancellationToken);
            return new ScheduledMessageHandle<T>(id, scheduledTime, destinationAddress, message);
        }

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            T message, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken) where T : class
        {
            return await ScheduleSend(destinationAddress, scheduledTime, message, cancellationToken);
        }

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            T message, IPipe<SendContext> pipe, CancellationToken cancellationToken) where T : class
        {
            return await ScheduleSend(destinationAddress, scheduledTime, message, cancellationToken);
        }

        // --- ScheduleSend (non-generic, object message) ---

        public async Task<ScheduledMessage> ScheduleSend(Uri destinationAddress, DateTime scheduledTime,
            object message, CancellationToken cancellationToken)
        {
            return await ScheduleSend(destinationAddress, scheduledTime, message, message.GetType(),
                cancellationToken);
        }

        public async Task<ScheduledMessage> ScheduleSend(Uri destinationAddress, DateTime scheduledTime,
            object message, Type messageType, CancellationToken cancellationToken)
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(messageType, message, ScheduledMessageMode.Send, destinationAddress),
                cancellationToken);
            return new ScheduledMessageHandle(id, scheduledTime, destinationAddress);
        }

        public async Task<ScheduledMessage> ScheduleSend(Uri destinationAddress, DateTime scheduledTime,
            object message, IPipe<SendContext> pipe, CancellationToken cancellationToken)
        {
            return await ScheduleSend(destinationAddress, scheduledTime, message, cancellationToken);
        }

        public async Task<ScheduledMessage> ScheduleSend(Uri destinationAddress, DateTime scheduledTime,
            object message, Type messageType, IPipe<SendContext> pipe, CancellationToken cancellationToken)
        {
            return await ScheduleSend(destinationAddress, scheduledTime, message, messageType, cancellationToken);
        }

        // --- ScheduleSend (generic, object values / message initializer) ---

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            object values, CancellationToken cancellationToken) where T : class
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(typeof(T), values, ScheduledMessageMode.Send, destinationAddress),
                cancellationToken);
            return new ScheduledMessageHandle<T>(id, scheduledTime, destinationAddress, default);
        }

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            object values, IPipe<SendContext<T>> pipe, CancellationToken cancellationToken) where T : class
        {
            return await ScheduleSend<T>(destinationAddress, scheduledTime, values, cancellationToken);
        }

        public async Task<ScheduledMessage<T>> ScheduleSend<T>(Uri destinationAddress, DateTime scheduledTime,
            object values, IPipe<SendContext> pipe, CancellationToken cancellationToken) where T : class
        {
            return await ScheduleSend<T>(destinationAddress, scheduledTime, values, cancellationToken);
        }

        // --- CancelScheduledSend ---

        public Task CancelScheduledSend(Uri destinationAddress, Guid tokenId,
            CancellationToken cancellationToken)
        {
            return _timeTickerManager.DeleteAsync(tokenId, cancellationToken);
        }

        // --- SchedulePublish (generic, typed message) ---

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, T message,
            CancellationToken cancellationToken) where T : class
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(typeof(T), message, ScheduledMessageMode.Publish),
                cancellationToken);
            return new ScheduledMessageHandle<T>(id, scheduledTime, null, message);
        }

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, T message,
            IPipe<SendContext<T>> pipe, CancellationToken cancellationToken) where T : class
        {
            return await SchedulePublish(scheduledTime, message, cancellationToken);
        }

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, T message,
            IPipe<SendContext> pipe, CancellationToken cancellationToken) where T : class
        {
            return await SchedulePublish(scheduledTime, message, cancellationToken);
        }

        // --- SchedulePublish (non-generic, object message) ---

        public async Task<ScheduledMessage> SchedulePublish(DateTime scheduledTime, object message,
            CancellationToken cancellationToken)
        {
            return await SchedulePublish(scheduledTime, message, message.GetType(), cancellationToken);
        }

        public async Task<ScheduledMessage> SchedulePublish(DateTime scheduledTime, object message,
            Type messageType, CancellationToken cancellationToken)
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(messageType, message, ScheduledMessageMode.Publish),
                cancellationToken);
            return new ScheduledMessageHandle(id, scheduledTime, null);
        }

        public async Task<ScheduledMessage> SchedulePublish(DateTime scheduledTime, object message,
            IPipe<SendContext> pipe, CancellationToken cancellationToken)
        {
            return await SchedulePublish(scheduledTime, message, cancellationToken);
        }

        public async Task<ScheduledMessage> SchedulePublish(DateTime scheduledTime, object message,
            Type messageType, IPipe<SendContext> pipe, CancellationToken cancellationToken)
        {
            return await SchedulePublish(scheduledTime, message, messageType, cancellationToken);
        }

        // --- SchedulePublish (generic, object values / message initializer) ---

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, object values,
            CancellationToken cancellationToken) where T : class
        {
            var id = await ScheduleViaTickerQ(scheduledTime,
                ScheduledMessagePayload.Create(typeof(T), values, ScheduledMessageMode.Publish),
                cancellationToken);
            return new ScheduledMessageHandle<T>(id, scheduledTime, null, default);
        }

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, object values,
            IPipe<SendContext<T>> pipe, CancellationToken cancellationToken) where T : class
        {
            return await SchedulePublish<T>(scheduledTime, values, cancellationToken);
        }

        public async Task<ScheduledMessage<T>> SchedulePublish<T>(DateTime scheduledTime, object values,
            IPipe<SendContext> pipe, CancellationToken cancellationToken) where T : class
        {
            return await SchedulePublish<T>(scheduledTime, values, cancellationToken);
        }

        // --- CancelScheduledPublish ---

        public Task CancelScheduledPublish<T>(Guid tokenId, CancellationToken cancellationToken) where T : class
        {
            return _timeTickerManager.DeleteAsync(tokenId, cancellationToken);
        }

        public Task CancelScheduledPublish(Type messageType, Guid tokenId,
            CancellationToken cancellationToken)
        {
            return _timeTickerManager.DeleteAsync(tokenId, cancellationToken);
        }

        // --- Internal helpers ---

        private async Task<Guid> ScheduleViaTickerQ(DateTime scheduledTime, ScheduledMessagePayload payload,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Scheduling {Mode} message for {ScheduledTime} via TickerQ",
                payload.Mode, scheduledTime);

            var result = await _timeTickerManager.AddAsync(new TimeTickerEntity
            {
                Function = _functionName,
                ExecutionTime = scheduledTime,
                Request = TickerHelper.CreateTickerRequest(payload)
            }, cancellationToken);
            return result.Result.Id;
        }

        private class ScheduledMessageHandle<T> : ScheduledMessage<T> where T : class
        {
            public ScheduledMessageHandle(Guid tokenId, DateTime scheduledTime, Uri destination, T payload)
            {
                TokenId = tokenId;
                ScheduledTime = scheduledTime;
                Destination = destination;
                Payload = payload;
            }

            public Guid TokenId { get; }
            public DateTime ScheduledTime { get; }
            public Uri Destination { get; }
            public T Payload { get; }
        }

        private class ScheduledMessageHandle : ScheduledMessage
        {
            public ScheduledMessageHandle(Guid tokenId, DateTime scheduledTime, Uri destination)
            {
                TokenId = tokenId;
                ScheduledTime = scheduledTime;
                Destination = destination;
            }

            public Guid TokenId { get; }
            public DateTime ScheduledTime { get; }
            public Uri Destination { get; }
        }
    }
}
