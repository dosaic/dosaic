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
}
