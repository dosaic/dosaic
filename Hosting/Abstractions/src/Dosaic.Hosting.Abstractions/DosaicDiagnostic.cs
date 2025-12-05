using System.Diagnostics;

namespace Dosaic.Hosting.Abstractions
{
    public static class DosaicDiagnostic
    {
        public const string DosaicActivityPrefix = "Dosaic.";
        public const string DosaicAllActivities = DosaicActivityPrefix + "*";

        public static ActivitySource CreateSource()
        {
            var frame = new StackFrame(1);
            var method = frame.GetMethod();
            var className = method?.DeclaringType?.FullName ?? "UnknownClass";
            return new(className);
        }
    }
}
