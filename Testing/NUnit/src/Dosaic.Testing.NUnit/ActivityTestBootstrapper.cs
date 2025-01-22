using System.Diagnostics;

namespace Dosaic.Testing.NUnit
{
    public static class ActivityTestBootstrapper
    {
        private static readonly ActivityListener _listener = new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        public static void Setup()
        {
            ActivitySource.AddActivityListener(_listener);
        }
    }
}
