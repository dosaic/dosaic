using System.Diagnostics;
using Dosaic.Hosting.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.Hangfire.Job
{
    public abstract class AsyncHangfireJob : HangfireJob
    {
        protected AsyncHangfireJob(ILogger logger) : base(logger)
        {
        }

        protected async Task<object> InternalExecuteAsync(Func<Activity, Task<object>> action)
        {
            using var activity = Tracing.StartActivity(GetType().FullName);
            using var scope = Logger.BeginScope(new Dictionary<string, object>
            {
                ["job.type"] = GetType().Name
            });
            try
            {
                var result = await action(activity).ConfigureAwait(false);
                activity?.SetOkStatus();
                return result;
            }
            catch (Exception exception)
            {
                activity?.SetErrorStatus(exception);
                Logger.LogError(exception, "Job failed");
                throw;
            }
        }
    }
}
