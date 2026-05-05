using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.Hangfire.Job
{
    public abstract class AsyncJob : AsyncHangfireJob, IAsyncJob
    {
        protected AsyncJob(ILogger logger) : base(logger)
        {
        }

        public Task<object> ExecuteAsync(CancellationToken jobCancellationToken = default)
        {
            return InternalExecuteAsync(async activity =>
            {
                EnrichActivity(activity);
                if (Timeout == null)
                {
                    return await ExecuteJobAsync(jobCancellationToken);
                }

                using var cts =
                    CancellationTokenSource.CreateLinkedTokenSource(jobCancellationToken);
                cts.CancelAfter(Timeout.Value);
                return await ExecuteJobAsync(cts.Token);
            });
        }

        protected abstract Task<object> ExecuteJobAsync(CancellationToken cancellationToken);

        /// <summary>
        ///     Override to attach span tags (e.g. business identifiers) before
        ///     <see cref="ExecuteJobAsync" /> runs. Default is a no-op.
        /// </summary>
        protected virtual void EnrichActivity(Activity activity) { }
    }
}
