using System.Diagnostics.Metrics;
using Dosaic.Hosting.Abstractions.Metrics;
using Serilog.Core;
using Serilog.Events;

namespace Dosaic.Hosting.WebHost.Logging
{
    internal class LoggingMetricSink : ILogEventSink
    {
        public const string MonitoringLogging = "monitoring_logging";
        public const string MonitoringLoggingException = "monitoring_logging_exception";

        internal readonly Counter<long> _loggingCounter = Metrics.CreateCounter<long>(MonitoringLogging, "calls", "count of logged messages");
        internal readonly Counter<long> _exceptionCounter = Metrics.CreateCounter<long>(MonitoringLoggingException, "calls", "count of exceptions logged");

        public void Emit(LogEvent logEvent)
        {
            _loggingCounter.Add(1, tag: new("log_level", logEvent.Level.ToString()));
            if (logEvent.Level is not (LogEventLevel.Error or LogEventLevel.Fatal)
                || logEvent.Exception is null) return;
            _exceptionCounter.Add(1, tag1: new("log_level", logEvent.Level.ToString()), new("exception_type", logEvent.Exception.GetType().Name));
        }
    }
}
