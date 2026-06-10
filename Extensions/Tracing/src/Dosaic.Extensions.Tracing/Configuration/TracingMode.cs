using Metalama.Framework.Aspects;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>Controls which methods are traced when the global <see cref="TracingFabric" /> is active.</summary>
    [RunTimeOrCompileTime]
    public enum TracingMode
    {
        /// <summary>All public methods on non-static classes.</summary>
        AllPublic,

        /// <summary>All methods (public + internal + private) on non-static classes.</summary>
        All,

        /// <summary>Only public methods returning Task / Task&lt;T&gt; / ValueTask / ValueTask&lt;T&gt;.</summary>
        PublicAsync,

        /// <summary>Only methods/classes explicitly marked with [Trace].</summary>
        AttributeOnly
    }
}
