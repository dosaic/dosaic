using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    [UniquePerQueue("unique")]
    public class UniqueTestJob : AsyncJob
    {
        public UniqueTestJob(ILogger logger) : base(logger) { }

        protected override Task<object> ExecuteJobAsync(CancellationToken cancellationToken) =>
            Task.FromResult<object>(new { test = true });
    }

    [UniquePerQueue("unique", CheckScheduledJobs = true)]
    public class UniqueScheduledTestJob : AsyncJob
    {
        public UniqueScheduledTestJob(ILogger logger) : base(logger) { }

        protected override Task<object> ExecuteJobAsync(CancellationToken cancellationToken) =>
            Task.FromResult<object>(new { test = true });
    }

    [UniquePerQueue("unique")]
    public class UniqueParamTestJob : ParameterizedAsyncJob<string>
    {
        public UniqueParamTestJob(ILogger logger) : base(logger) { }

        protected override Task<object> ExecuteJobAsync(string value, CancellationToken cancellationToken) =>
            Task.FromResult<object>(new { test = true, Val = value });
    }
}
