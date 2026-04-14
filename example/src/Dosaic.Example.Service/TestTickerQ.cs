using Dosaic.Plugins.Jobs.TickerQ;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;

namespace Dosaic.Example.Service
{
    public class TestTickerQ : ITickerQConfigurator
    {
        public bool IncludesPersistence => false;

        public void Configure(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options)
        {
            // custom TickerQ options can be applied here
        }
    }

    public class TestTickerJob
    {
        private readonly ILogger<TestTickerJob> _logger;

        public TestTickerJob(ILogger<TestTickerJob> logger)
        {
            _logger = logger;
        }

        [TickerFunction("TestTicker", "*/1 * * * *")]
        public Task Execute(TickerFunctionContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("TickerQ test job executed");
            return Task.CompletedTask;
        }
    }
}
