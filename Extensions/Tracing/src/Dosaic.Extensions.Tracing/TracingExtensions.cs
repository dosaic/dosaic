using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dosaic.Hosting.Abstractions;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>
    ///     Static extension members on <see cref="Dosaic.Hosting.Abstractions.Tracing" />.
    ///     <para>
    ///         <c>StartSpan</c> / <c>Trace</c> / <c>TraceAsync</c> start an opt-in span named
    ///         <c>TypeName.MethodName</c> on the shared <c>Dosaic</c> <see cref="ActivitySource" />.
    ///     </para>
    ///     <para>
    ///         <c>Tag</c> / <c>Event</c> / <c>Error</c> / <c>Link</c> enrich <see cref="Activity.Current" /> and
    ///         never start a new activity, so they are no-ops when no activity is active.
    ///     </para>
    /// </summary>
    public static class TracingExtensions
    {
        extension(Dosaic.Hosting.Abstractions.Tracing)
        {
            /// <summary>The ambient activity (null if none is active).</summary>
            public static Activity? Current => Activity.Current;

            /// <summary>
            ///     Start a span named <c>{owner type}.{calling member}</c>. Dispose it with <c>using</c>.
            ///     Returns null when nothing listens to the <c>Dosaic</c> source.
            /// </summary>
            public static Activity? StartSpan(object owner, [CallerMemberName] string method = "",
                ActivityKind kind = ActivityKind.Internal)
                => Dosaic.Hosting.Abstractions.Tracing.Source.StartActivity(SpanName(owner.GetType(), method), kind);

            /// <summary>
            ///     Start a span named <c>{TOwner}.{calling member}</c>, for static members where there is no
            ///     <c>this</c>. Dispose it with <c>using</c>.
            /// </summary>
            public static Activity? StartSpan<TOwner>([CallerMemberName] string method = "",
                ActivityKind kind = ActivityKind.Internal)
                => Dosaic.Hosting.Abstractions.Tracing.Source.StartActivity(SpanName(typeof(TOwner), method), kind);

            /// <summary>Run <paramref name="action" /> in a span; an exception marks the span as error and is re-thrown.</summary>
            public static void Trace(object owner, Action<Activity?> action, [CallerMemberName] string method = "")
            {
                using var activity = Dosaic.Hosting.Abstractions.Tracing.StartSpan(owner, method);
                try
                {
                    action(activity);
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }

            /// <summary>Run <paramref name="func" /> in a span; an exception marks the span as error and is re-thrown.</summary>
            public static T Trace<T>(object owner, Func<Activity?, T> func, [CallerMemberName] string method = "")
            {
                using var activity = Dosaic.Hosting.Abstractions.Tracing.StartSpan(owner, method);
                try
                {
                    return func(activity);
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }

            /// <summary>Await <paramref name="func" /> in a span; an exception marks the span as error and is re-thrown.</summary>
            public static async Task TraceAsync(object owner, Func<Activity?, Task> func,
                [CallerMemberName] string method = "")
            {
                using var activity = Dosaic.Hosting.Abstractions.Tracing.StartSpan(owner, method);
                try
                {
                    await func(activity);
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }

            /// <summary>Await <paramref name="func" /> in a span; an exception marks the span as error and is re-thrown.</summary>
            public static async Task<T> TraceAsync<T>(object owner, Func<Activity?, Task<T>> func,
                [CallerMemberName] string method = "")
            {
                using var activity = Dosaic.Hosting.Abstractions.Tracing.StartSpan(owner, method);
                try
                {
                    return await func(activity);
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }

            /// <summary>Add a tag to the current span.</summary>
            public static void Tag(string key, object? value)
                => Activity.Current?.SetTag(key, value);

            /// <summary>Add an event to the current span.</summary>
            public static void Event(string name, params KeyValuePair<string, object?>[] tags)
                => Activity.Current?.AddEvent(new ActivityEvent(name, tags: new ActivityTagsCollection(tags)));

            /// <summary>Record an exception on the current span (does not re-throw).</summary>
            public static void Error(Exception ex)
                => Activity.Current?.SetErrorStatus(ex);

            /// <summary>Add a link to another trace.</summary>
            public static void Link(ActivityContext target, string? description = null)
                => Activity.Current?.AddLink(new ActivityLink(target,
                    description is not null
                        ? new ActivityTagsCollection([new KeyValuePair<string, object?>("link.description", description)])
                        : null));
        }

        private static string SpanName(Type owner, string method)
            => $"{Dosaic.Hosting.Abstractions.Tracing.DisplayName(owner)}.{method}";
    }
}
