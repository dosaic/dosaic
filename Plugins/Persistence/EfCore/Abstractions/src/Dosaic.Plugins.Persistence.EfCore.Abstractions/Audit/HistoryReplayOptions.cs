using System.Reflection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Options that customise how <see cref="HistoryReplay"/> rebuilds entities from history change sets.
    /// </summary>
    public sealed class HistoryReplayOptions
    {
        public static readonly HistoryReplayOptions Default = new();

        /// <summary>
        /// Optional app-specific value conversion. Invoked for every value assigned to a property during replay
        /// (root properties, child-collection snapshots and child properties), before the built-in conversion.
        /// Receives the target property and the raw change-set value (e.g. a JSON string) and returns the value to
        /// assign. Return the input unchanged to fall back to the built-in conversion.
        /// </summary>
        public Func<PropertyInfo, object, object> ValueConverter { get; init; }
    }
}
