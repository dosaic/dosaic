using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace Dosaic.Plugins.Jobs.TickerQ
{
    internal class JobManager : IJobManager
    {
        private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
        private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager;

        public JobManager(ITimeTickerManager<TimeTickerEntity> timeTickerManager,
            ICronTickerManager<CronTickerEntity> cronTickerManager)
        {
            _timeTickerManager = timeTickerManager;
            _cronTickerManager = cronTickerManager;
        }

        public async Task<Guid> EnqueueAsync(string functionName, CancellationToken cancellationToken)
        {
            var result = await _timeTickerManager.AddAsync(new TimeTickerEntity
            {
                Function = functionName,
                ExecutionTime = DateTime.UtcNow
            }, cancellationToken);
            return result.Result.Id;
        }

        public async Task<Guid> EnqueueAsync<TRequest>(string functionName, TRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _timeTickerManager.AddAsync(new TimeTickerEntity
            {
                Function = functionName,
                ExecutionTime = DateTime.UtcNow,
                Request = TickerHelper.CreateTickerRequest(request)
            }, cancellationToken);
            return result.Result.Id;
        }

        public async Task<Guid> ScheduleAsync(string functionName, TimeSpan delay,
            CancellationToken cancellationToken)
        {
            return await ScheduleAsync(functionName, DateTime.UtcNow.Add(delay), cancellationToken);
        }

        public async Task<Guid> ScheduleAsync(string functionName, DateTime executionTime,
            CancellationToken cancellationToken)
        {
            var result = await _timeTickerManager.AddAsync(new TimeTickerEntity
            {
                Function = functionName,
                ExecutionTime = executionTime
            }, cancellationToken);
            return result.Result.Id;
        }

        public async Task<Guid> ScheduleAsync<TRequest>(string functionName, TRequest request,
            TimeSpan delay, CancellationToken cancellationToken)
        {
            return await ScheduleAsync(functionName, request, DateTime.UtcNow.Add(delay),
                cancellationToken);
        }

        public async Task<Guid> ScheduleAsync<TRequest>(string functionName, TRequest request,
            DateTime executionTime, CancellationToken cancellationToken)
        {
            var result = await _timeTickerManager.AddAsync(new TimeTickerEntity
            {
                Function = functionName,
                ExecutionTime = executionTime,
                Request = TickerHelper.CreateTickerRequest(request)
            }, cancellationToken);
            return result.Result.Id;
        }

        public async Task RegisterRecurringAsync(string functionName, string cronExpression,
            CancellationToken cancellationToken)
        {
            await _cronTickerManager.AddAsync(new CronTickerEntity
            {
                Function = functionName,
                Expression = cronExpression,
                IsEnabled = true
            }, cancellationToken);
        }

        public async Task DeleteRecurringAsync(Guid id, CancellationToken cancellationToken)
        {
            await _cronTickerManager.DeleteAsync(id, cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            await _timeTickerManager.DeleteAsync(id, cancellationToken);
        }
    }
}
