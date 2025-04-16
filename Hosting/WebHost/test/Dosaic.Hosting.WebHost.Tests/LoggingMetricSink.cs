using Dosaic.Hosting.Abstractions.Metrics;
using Dosaic.Hosting.WebHost.Logging;
using FluentAssertions;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Serilog.Events;
using Serilog.Parsing;

namespace Dosaic.Hosting.WebHost.Tests
{
    public class LoggingMetricSink
    {
        private WebHost.Logging.LoggingMetricSink _sink = null!;

        [SetUp]
        public void Init()
        {
            _sink = new WebHost.Logging.LoggingMetricSink();
        }

        private static LogEvent CreateLogEntry(LogEventLevel level, string message, Exception exception = null) =>
            new(DateTimeOffset.UtcNow, level, exception,
                new MessageTemplate(message, new List<MessageTemplateToken>()), new List<LogEventProperty>());

        [Test]
        public void SetsMetricForLogMessage()
        {
            // Inspired by
            // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/test/OpenTelemetry.Tests/Metrics/MetricApiTests.cs
            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(Metrics.Meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var logEntry = CreateLogEntry(LogEventLevel.Information, "Test");
            _sink.Emit(logEntry);
            _sink.Emit(logEntry);
            meterProvider.ForceFlush(10000);

            var metric = exportedItems.First(x => x.Name == WebHost.Logging.LoggingMetricSink.MonitoringLogging);
            metric.Should().NotBeNull();
            metric.Temporality.Should().Be(AggregationTemporality.Cumulative);

            List<MetricPoint> metricPoints = [];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            var metricPoint = metricPoints[0];
            metricPoint.GetSumLong().Should().Be(2);
        }

        [Test]
        public void SetsMetricForLogMessageWithException()
        {
            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(Metrics.Meter.Name)
                .AddInMemoryExporter(exportedItems)
                .Build();


            var logEntry = CreateLogEntry(LogEventLevel.Error, "Test", new ApplicationException("test"));
            _sink.Emit(logEntry);
            _sink.Emit(logEntry);
            meterProvider.ForceFlush(10000);
            var metric = exportedItems.First(x => x.Name == WebHost.Logging.LoggingMetricSink.MonitoringLoggingException);
            metric.Should().NotBeNull();
            metric.Temporality.Should().Be(AggregationTemporality.Cumulative);

            List<MetricPoint> metricPoints = [];
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            var metricPoint = metricPoints[0];
            metricPoint.GetSumLong().Should().Be(2);
        }
    }
}
