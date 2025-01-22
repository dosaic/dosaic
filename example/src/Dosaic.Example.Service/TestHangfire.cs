using Dosaic.Plugins.Jobs.Hangfire;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.Logging;

namespace Dosaic.Example.Service
{
    public class TestHangfire : IHangfireConfigurator
    {
        public bool IncludesStorage => false;

        public void Configure(IGlobalConfiguration config)
        {
            config.UseMemoryStorage();
        }

        public void ConfigureServer(BackgroundJobServerOptions options)
        {
            options.WorkerCount = 1;
        }
    }

    [RecurringJob("*/1 * * * *")]
    public class TestJob : AsyncJob
    {
        public TestJob(ILogger logger) : base(logger)
        {
        }

        protected override Task<object> ExecuteJobAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<object>(new { Hello = "World" });
        }
    }
}
