using Polly;

namespace Dosaic.Extensions.RestEase.Caching
{
    internal sealed class HttpCacheStrategyOptions : ResilienceStrategyOptions
    {
        public HttpCacheStrategyOptions() { Name = "HttpCache"; }
    }
}
