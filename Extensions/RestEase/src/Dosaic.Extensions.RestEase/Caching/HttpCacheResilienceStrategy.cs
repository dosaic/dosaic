using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace Dosaic.Extensions.RestEase.Caching
{
    internal sealed class HttpCacheResilienceStrategy : ResilienceStrategy<HttpResponseMessage>
    {
        private readonly IDistributedCache _cache;
        private readonly HttpCacheOptions _options;
        private readonly ILogger _logger;

        public HttpCacheResilienceStrategy(IDistributedCache cache, HttpCacheOptions options, ILogger logger = null)
        {
            _cache = cache;
            _options = options;
            _logger = logger ?? NullLogger.Instance;
        }

        protected override async ValueTask<Outcome<HttpResponseMessage>> ExecuteCore<TState>(
            Func<ResilienceContext, TState, ValueTask<Outcome<HttpResponseMessage>>> callback,
            ResilienceContext context,
            TState state)
        {
            var request = context.GetRequestMessage();
            if (request is null || !ShouldCache(request))
                return await callback(context, state);

            var key = BuildKey(request);
            var bytes = await _cache.GetAsync(key, context.CancellationToken);
            if (bytes is { Length: > 0 })
            {
                _logger.LogDebug("RestEase cache hit {Key}", key);
                return Outcome.FromResult(HttpCacheEntry.Deserialize(bytes).ToResponse(request));
            }

            var outcome = await callback(context, state);
            if (outcome.Exception is null && outcome.Result is { } response && CanStore(response))
            {
                var entry = await HttpCacheEntry.FromResponseAsync(response, context.CancellationToken);
                var ttl = ResolveTtl(response);
                if (ttl > TimeSpan.Zero)
                {
                    await _cache.SetAsync(key, entry.Serialize(),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                        context.CancellationToken);
                    _logger.LogDebug("RestEase cache stored {Key} ttl={Ttl}", key, ttl);
                }
            }
            return outcome;
        }

        private bool ShouldCache(HttpRequestMessage request)
        {
            if (!_options.Enabled) return false;
            if (!_options.Methods.Contains(request.Method.Method)) return false;
            var cc = request.Headers.CacheControl;
            if (cc is { NoStore: true } or { NoCache: true }) return false;
            return _options.ShouldCacheRequest?.Invoke(request) ?? true;
        }

        private bool CanStore(HttpResponseMessage response)
        {
            if (!_options.CacheableStatusCodes.Contains((int)response.StatusCode)) return false;
            if (_options.RespectCacheControl)
            {
                var cc = response.Headers.CacheControl;
                if (cc is { NoStore: true } or { NoCache: true } or { Private: true }) return false;
            }
            return _options.ShouldCacheResponse?.Invoke(response) ?? true;
        }

        private TimeSpan ResolveTtl(HttpResponseMessage response)
        {
            var ttl = _options.DefaultTtl;
            if (_options.RespectCacheControl && response.Headers.CacheControl?.MaxAge is { } maxAge)
                ttl = maxAge;
            if (_options.MaxTtl is { } max && ttl > max)
                ttl = max;
            return ttl;
        }

        private string BuildKey(HttpRequestMessage request)
        {
            if (_options.KeyBuilder is not null)
                return _options.KeyPrefix + _options.KeyBuilder(request);

            var sb = new StringBuilder();
            sb.Append(request.Method.Method).Append(' ').Append(request.RequestUri);
            if (_options.IncludeAuthorizationInKey && request.Headers.Authorization is { } auth)
                sb.Append('|').Append(HashAuth(auth));
            return _options.KeyPrefix + sb;
        }

        private static string HashAuth(AuthenticationHeaderValue auth)
        {
            var raw = auth.Scheme + " " + auth.Parameter;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }
    }
}
