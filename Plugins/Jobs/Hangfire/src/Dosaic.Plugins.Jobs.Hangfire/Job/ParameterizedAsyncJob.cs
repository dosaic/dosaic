using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.Hangfire.Job
{
    public abstract class ParameterizedAsyncJob<T> : AsyncHangfireJob, IParameterizedAsyncJob<T>
    {
        protected ParameterizedAsyncJob(ILogger logger) : base(logger)
        {
        }

        public Task<object> ExecuteAsync(T value, CancellationToken jobCancellationToken = default)
        {
            return InternalExecuteAsync(async activity =>
            {
                EnrichActivity(activity, value);
                if (Timeout == null)
                {
                    return await ExecuteJobAsync(value, jobCancellationToken);
                }

                using var cts =
                    CancellationTokenSource.CreateLinkedTokenSource(jobCancellationToken);
                cts.CancelAfter(Timeout.Value);
                return await ExecuteJobAsync(value, cts.Token);
            });
        }

        protected abstract Task<object> ExecuteJobAsync(T value, CancellationToken cancellationToken);

        /// <summary>
        ///     Override to attach span tags derived from <paramref name="value" />
        ///     before <see cref="ExecuteJobAsync" /> runs. Default is a no-op.
        /// </summary>
        protected virtual void EnrichActivity(Activity activity, T value) { }
    }
}
