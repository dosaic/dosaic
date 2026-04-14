using System.Collections.Concurrent;
using Dosaic.Hosting.Abstractions.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Jobs.TickerQ
{
    public sealed class TickerQStatisticsMetricsReporter : IHostedService, IDisposable
    {
        private const string MetricName = "tickerq_job_count";
        private static readonly ConcurrentDictionary<string, long> _metricValues = new();

        private readonly ILogger<TickerQStatisticsMetricsReporter> _logger;
        private Timer _timer = null!;

        public TickerQStatisticsMetricsReporter(ILogger<TickerQStatisticsMetricsReporter> logger)
        {
            _logger = logger;
            SetupGauges();
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(CollectData!, null, 1000, 60000);
            return Task.CompletedTask;
        }

        internal void CollectData(object state)
        {
            try
            {
                // TickerQ does not expose a statistics API directly;
                // gauge values remain at 0 until a persistence-aware reporter is registered.
                // The OpenTelemetry instrumentation package provides tracing spans for
                // individual job executions. This reporter supplements with aggregate counters.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while gathering TickerQ metrics");
            }
        }

        private static void SetupGauges()
        {
            var metricNames = new[] { "Idle", "Queued", "InProgress", "Done", "Failed", "Cancelled", "Skipped" };
            foreach (var metricName in metricNames)
            {
                _metricValues.TryAdd(metricName, 0);
                Metrics.CreateObservableGauge($"{MetricName}_{metricName}", () => ProvideMetric(metricName), "jobs", "TickerQ job count");
            }
        }

        private static long ProvideMetric(string name)
        {
            _metricValues.TryGetValue(name, out var value);
            return value;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }
    }
}
