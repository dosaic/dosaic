using System.Collections.Concurrent;
using Dosaic.Plugins.Jobs.Hangfire.Job;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    /// <summary>
    ///     Records every execution so the integration tests can assert that batched jobs really run.
    ///     Needs a public parameterless constructor because Hangfire's default job activator is used.
    /// </summary>
    public class RecordingJob : IParameterizedAsyncJob<string>
    {
        public static readonly ConcurrentQueue<string> Executed = new();

        public Task<object> ExecuteAsync(string value, CancellationToken jobCancellationToken = default)
        {
            Executed.Enqueue(value);
            return Task.FromResult<object>(value);
        }

        public void Dispose() => GC.SuppressFinalize(this);
    }

    public class NoopJob : IAsyncJob
    {
        public Task<object> ExecuteAsync(CancellationToken jobCancellationToken = default) =>
            Task.FromResult<object>(null);

        public void Dispose() => GC.SuppressFinalize(this);
    }
}
