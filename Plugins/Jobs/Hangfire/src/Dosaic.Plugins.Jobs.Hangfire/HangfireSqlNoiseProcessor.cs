using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    /// <summary>
    ///     Suppresses Activities that represent Hangfire's PostgreSQL polling
    ///     traffic so the OTLP exporter does not flood traces with infrastructure
    ///     noise. Detects them by scanning every common SQL-bearing tag plus the
    ///     Activity's DisplayName / OperationName for a Hangfire schema marker.
    ///     Different DB instrumentations use different tag keys and quoting styles
    ///     (e.g. <c>db.statement</c> with quoted identifiers vs <c>db.query.text</c>
    ///     with unquoted ones), so the matcher is intentionally permissive.
    /// </summary>
    public sealed class HangfireSqlNoiseProcessor : BaseProcessor<Activity>
    {
        internal const string QuotedHangfireSchemaMarker = "\"hangfire\".";
        internal const string UnquotedHangfireSchemaMarker = "hangfire.";

        private static readonly string[] _statementTagKeys =
        [
            "db.statement",
            "db.query.text",
            "db.statement.text"
        ];

        public override void OnEnd(Activity data)
        {
            if (ShouldSuppress(data))
            {
                // Clearing the Recorded flag causes any downstream processor (e.g.
                // BatchExportProcessor) to skip this Activity and prevents it from
                // reaching the exporter — provided we run before the exporter.
                data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
            base.OnEnd(data);
        }

        public static bool ShouldSuppress(Activity activity)
        {
            if (activity is null) return false;

            foreach (var tagKey in _statementTagKeys)
            {
                if (activity.GetTagItem(tagKey) is string value && ContainsHangfireMarker(value))
                {
                    return true;
                }
            }

            if (ContainsHangfireMarker(activity.DisplayName)) return true;
            if (ContainsHangfireMarker(activity.OperationName)) return true;

            return false;
        }

        private static bool ContainsHangfireMarker(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.Contains(QuotedHangfireSchemaMarker, StringComparison.OrdinalIgnoreCase)
                   || value.Contains(UnquotedHangfireSchemaMarker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Registers <see cref="HangfireSqlNoiseProcessor" /> as the *first*
        ///     OpenTelemetry processor by appending the
        ///     <c>ConfigureOpenTelemetryTracerProvider</c> descriptor and then
        ///     moving every newly added descriptor to the head of the
        ///     <see cref="IServiceCollection" />. The OTel SDK invokes
        ///     <c>IConfigureTracerProviderBuilder</c> callbacks in DI registration
        ///     order; running first is required so the noise filter can clear the
        ///     <see cref="ActivityTraceFlags.Recorded" /> flag before the OTLP
        ///     <c>BatchExportProcessor</c> enqueues the activity.
        /// </summary>
        public static void RegisterFirst(IServiceCollection services)
        {
            var indexBefore = services.Count;
            services.ConfigureOpenTelemetryTracerProvider((_, builder) =>
                builder.AddProcessor(new HangfireSqlNoiseProcessor()));
            for (var i = services.Count - 1; i >= indexBefore; i--)
            {
                var descriptor = services[i];
                services.RemoveAt(i);
                services.Insert(0, descriptor);
            }
        }
    }
}
