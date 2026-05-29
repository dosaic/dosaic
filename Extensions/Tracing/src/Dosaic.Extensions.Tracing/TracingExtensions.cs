using System.Diagnostics;
using Dosaic.Hosting.Abstractions;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>
    ///     Static extension members on <see cref="Tracing" /> for enriching the current span from inside a
    ///     traced method. They never start a new activity — they work with <see cref="Activity.Current" /> only,
    ///     so calls are no-ops when no activity is active.
    /// </summary>
    public static class TracingExtensions
    {
        extension(Dosaic.Hosting.Abstractions.Tracing)
        {
            /// <summary>The ambient activity (null if none is active).</summary>
            public static Activity? Current => Activity.Current;

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
    }
}
