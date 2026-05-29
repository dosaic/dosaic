namespace Dosaic.Extensions.Tracing
{
    /// <summary>Excludes a class or method from automatic tracing.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NoTraceAttribute : Attribute
    {
    }
}
