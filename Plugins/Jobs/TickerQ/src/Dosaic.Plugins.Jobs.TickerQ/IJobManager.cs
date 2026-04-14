namespace Dosaic.Plugins.Jobs.TickerQ
{
    public interface IJobManager
    {
        Task<Guid> EnqueueAsync(string functionName, CancellationToken cancellationToken = default);

        Task<Guid> EnqueueAsync<TRequest>(string functionName, TRequest request,
            CancellationToken cancellationToken = default);

        Task<Guid> ScheduleAsync(string functionName, TimeSpan delay,
            CancellationToken cancellationToken = default);

        Task<Guid> ScheduleAsync(string functionName, DateTime executionTime,
            CancellationToken cancellationToken = default);

        Task<Guid> ScheduleAsync<TRequest>(string functionName, TRequest request, TimeSpan delay,
            CancellationToken cancellationToken = default);

        Task<Guid> ScheduleAsync<TRequest>(string functionName, TRequest request, DateTime executionTime,
            CancellationToken cancellationToken = default);

        Task RegisterRecurringAsync(string functionName, string cronExpression,
            CancellationToken cancellationToken = default);

        Task DeleteRecurringAsync(Guid id, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
