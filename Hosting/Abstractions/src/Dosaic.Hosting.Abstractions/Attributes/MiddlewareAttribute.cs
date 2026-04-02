namespace Dosaic.Hosting.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MiddlewareAttribute : Attribute
    {
        public MiddlewareAttribute(int order = int.MaxValue, MiddlewareMode mode = MiddlewareMode.BeforePlugins)
        {
            Order = order;
            Mode = mode;
        }

        public int Order { get; }
        public MiddlewareMode Mode { get; }
    }

    public enum MiddlewareMode
    {
        NoRegistration,
        BeforePlugins,
        AfterPlugins
    }
}
