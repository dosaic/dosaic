
namespace Dosaic.Extensions.Tracing.FabricFixture
{
    // No [Trace] anywhere — the global fabric (DosaicTracingMode=AllPublic) decides what is traced.
    public class AutoTracedService
    {
        public string PublicMethod(string value) => Helper(value);

        // Private → not eligible under AllPublic.
        private static string Helper(string value) => value;

        public async Task<int> PublicAsyncMethod(int seed)
        {
            await Task.Yield();
            return seed + 1;
        }

        [NoTrace]
        public string OptedOutMethod(string value) => value;
    }

    [NoTrace]
    public class FullyOptedOutService
    {
        public string Method(string value) => value;
    }
}

namespace Dosaic.Extensions.Tracing.FabricFixture.Excluded
{
    public class ExcludedService
    {
        public string Method(string value) => value;
    }
}
