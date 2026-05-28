using System.Net.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;

namespace Dosaic.Extensions.RestEase.Caching
{
    public static class HttpCachePollyExtensions
    {
        public static ResiliencePipelineBuilder<HttpResponseMessage> AddHttpCache(
            this ResiliencePipelineBuilder<HttpResponseMessage> builder,
            IDistributedCache cache,
            HttpCacheOptions options,
            ILogger logger = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(options);
            builder.AddStrategy(_ => new HttpCacheResilienceStrategy(cache, options, logger), new HttpCacheStrategyOptions());
            return builder;
        }
    }
}
