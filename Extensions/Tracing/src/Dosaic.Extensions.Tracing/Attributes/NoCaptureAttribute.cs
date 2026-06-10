namespace Dosaic.Extensions.Tracing
{
    /// <summary>Excludes a parameter from argument capture even when capture is enabled.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NoCaptureAttribute : Attribute
    {
    }
}
