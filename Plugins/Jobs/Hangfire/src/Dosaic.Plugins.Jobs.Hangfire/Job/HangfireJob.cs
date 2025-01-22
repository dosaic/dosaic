using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.Hangfire.Job
{
    public abstract class HangfireJob : IDisposable
    {
        protected readonly ILogger Logger;
        protected readonly TimeSpan? Timeout;

        protected HangfireJob(ILogger logger)
        {
            Logger = logger;
            Timeout = GetType().GetAttribute<JobTimeoutAttribute>()?.Timeout;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup
        }
    }
}
