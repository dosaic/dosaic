using System.Diagnostics.Metrics;
using Dosaic.Hosting.Abstractions.Metrics;
using Serilog.Core;
using Serilog.Events;

namespace Dosaic.Hosting.WebHost.Logging
{
    internal class LoggingMetricSink : ILogEventSink
    {
        internal Counter<long> loggingCounter = Metrics.CreateCounter<long>("monitoring_logging", "calls", "count of logged messages");
        internal Counter<long> exceptionCounter = Metrics.CreateCounter<long>("monitoring_logging_exception", "calls", "count of exceptions logged");

        public void Emit(LogEvent logEvent)
        {
            loggingCounter.Add(1, tag: new("log_level", logEvent.Level.ToString()));

            if (logEvent.Level is not (LogEventLevel.Error or LogEventLevel.Fatal)
                || logEvent.Exception is null) return;
            exceptionCounter.Add(1, tag1: new("log_level", logEvent.Level.ToString()), new("exception_type", logEvent.Exception.GetType().Name));
        }
    }
}
