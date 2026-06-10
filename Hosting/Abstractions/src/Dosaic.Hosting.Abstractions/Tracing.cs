using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dosaic.Hosting.Abstractions
{
    /// <summary>
    ///     Single entry point for OpenTelemetry tracing in Dosaic-based services.
    ///     Wraps one shared <see cref="ActivitySource" /> so that consumers never
    ///     instantiate their own. Span granularity is encoded in the span name,
    ///     not the source name.
    /// </summary>
    public static class Tracing
    {
        public const string SourceName = "Dosaic";

        public static ActivitySource Source { get; } = new(SourceName);

        /// <summary>
        ///     A human-readable type name for spans/tags. Strips the CLR arity backtick and renders
        ///     generic arguments, e.g. <c>EntityChange&lt;ImportShipment&gt;</c> instead of
        ///     <c>EntityChange`1</c>. Set <paramref name="fullName" /> to include the namespace.
        /// </summary>
        public static string DisplayName(this Type type, bool fullName = false)
        {
            if (type is null) return null;
            var raw = fullName && type.Namespace is not null ? $"{type.Namespace}.{type.Name}" : type.Name;
            var tick = raw.IndexOf('`');
            var name = tick < 0 ? raw : raw[..tick];
            if (!type.IsGenericType) return name;
            var args = string.Join(", ", type.GetGenericArguments().Select(a => a.DisplayName(fullName)));
            return $"{name}<{args}>";
        }

        public static Activity StartActivity(
            [CallerMemberName] string name = "",
            ActivityKind kind = ActivityKind.Internal)
            => Source.StartActivity(name, kind);

        public static Activity StartActivity(
            string name,
            ActivityKind kind,
            ActivityContext parentContext,
            IEnumerable<KeyValuePair<string, object>> tags = null,
            IEnumerable<ActivityLink> links = null,
            DateTimeOffset startTime = default)
            => Source.StartActivity(name, kind, parentContext, tags, links, startTime);

        /// <summary>
        ///     Detaches the ambient <see cref="Activity" /> so any downstream propagation (e.g. a
        ///     transport that writes the W3C <c>traceparent</c> header) emits nothing, making the
        ///     next operation its own root trace. The suppressed span is captured as
        ///     <see cref="TraceLinkScope.TraceParent" /> so it can be re-attached as an
        ///     <see cref="ActivityLink" /> on the far side; <see cref="Activity.Current" /> is
        ///     restored when the returned scope is disposed.
        /// </summary>
        public static TraceLinkScope SuppressForLinking()
        {
            var current = Activity.Current;
            var traceParent = current is { IdFormat: ActivityIdFormat.W3C } ? current.Id : null;
            Activity.Current = null;
            return new TraceLinkScope(current, traceParent);
        }

        /// <summary>
        ///     Attaches a W3C <c>traceparent</c> (as produced by <see cref="SuppressForLinking" /> or
        ///     <see cref="Activity.Id" />) to <paramref name="activity" /> as an
        ///     <see cref="ActivityLink" />. No-op when the activity is null or the value is not a
        ///     parseable traceparent string.
        /// </summary>
        public static Activity AddTraceLink(this Activity activity, object traceParent)
        {
            if (activity is null) return null;
            if (traceParent is string s && ActivityContext.TryParse(s, null, true, out var ctx))
                activity.AddLink(new ActivityLink(ctx));
            return activity;
        }

        public static async Task<T> TrackStatusAsync<T>(
            Func<Activity, Task<T>> func,
            [CallerMemberName] string activityName = "",
            ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = Source.StartActivity(activityName, kind);
            try
            {
                var result = await func(activity);
                activity?.SetOkStatus();
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetErrorStatus(ex);
                throw;
            }
        }

        public static async Task TrackStatusAsync(
            Func<Activity, Task> func,
            [CallerMemberName] string activityName = "",
            ActivityKind kind = ActivityKind.Internal)
        {
            using var activity = Source.StartActivity(activityName, kind);
            try
            {
                await func(activity);
                activity?.SetOkStatus();
            }
            catch (Exception ex)
            {
                activity?.SetErrorStatus(ex);
                throw;
            }
        }

        public static Activity SetTags(this Activity activity, Dictionary<string, string> tags, string prefix = "")
        {
            if (activity is null) return null;
            foreach (var (key, value) in tags)
            {
                activity.SetTag($"{prefix}{key}", value);
            }

            return activity;
        }

        public static Activity SetErrorStatus(this Activity activity, Exception ex)
        {
            if (activity is null) return null;
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.AddException(ex);
            return activity;
        }

        public static Activity SetOkStatus(this Activity activity)
        {
            if (activity is null) return null;
            activity.SetStatus(ActivityStatusCode.Ok);
            return activity;
        }
    }

    /// <summary>
    ///     Scope returned by <see cref="Tracing.SuppressForLinking" />: holds the suppressed span's
    ///     W3C <c>traceparent</c> (<c>null</c> when there was none) and restores
    ///     <see cref="Activity.Current" /> on dispose.
    /// </summary>
    public readonly struct TraceLinkScope(Activity previous, string traceParent) : IDisposable
    {
        public string TraceParent { get; } = traceParent;
        public void Dispose() => Activity.Current = previous;
    }
}
