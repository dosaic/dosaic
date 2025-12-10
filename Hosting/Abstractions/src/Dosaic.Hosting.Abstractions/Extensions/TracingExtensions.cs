using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class TracingExtensions
    {
        public static async Task<T> TrackStatusAsync<T>(
            this ActivitySource activitySource,
            Func<Activity, Task<T>> func,
            [CallerMemberName] string activityName = "")
        {
            using var activity = activitySource.StartActivity(activityName);
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
            this ActivitySource activitySource,
            Func<Activity, Task> func,
            [CallerMemberName] string activityName = "")
        {
            using var activity = activitySource.StartActivity(activityName);
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
                activity?.SetTag($"{prefix}{key}", value);
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
