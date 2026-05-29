using Metalama.Framework.Aspects;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>Strategy for capturing method arguments as span tags.</summary>
    [RunTimeOrCompileTime]
    public enum ArgCaptureMode
    {
        /// <summary>No parameter capture.</summary>
        None,

        /// <summary>Capture via .ToString().</summary>
        ToString,

        /// <summary>Capture via System.Text.Json serialization.</summary>
        Json
    }
}
