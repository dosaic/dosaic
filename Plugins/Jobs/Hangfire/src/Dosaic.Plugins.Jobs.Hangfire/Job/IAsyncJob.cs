namespace Dosaic.Plugins.Jobs.Hangfire.Job
{
    public interface IAsyncJob : IDisposable
    {
        Task<object> ExecuteAsync(CancellationToken jobCancellationToken = default);
    }
}
