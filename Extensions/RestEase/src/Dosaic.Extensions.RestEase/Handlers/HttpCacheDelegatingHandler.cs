using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Dosaic.Extensions.RestEase.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dosaic.Extensions.RestEase.Handlers
{
    public sealed class HttpCacheDelegatingHandler : DelegatingHandler
    {
        private readonly IDistributedCache _cache;
        private readonly HttpCacheOptions _options;
        private readonly ILogger<HttpCacheDelegatingHandler> _logger;

        public HttpCacheDelegatingHandler(IDistributedCache cache, IOptionsMonitor<HttpCacheOptions> monitor, string name)
            : this(cache, monitor.Get(name), null) { }

        internal HttpCacheDelegatingHandler(IDistributedCache cache, HttpCacheOptions options, ILogger<HttpCacheDelegatingHandler> logger)
        {
            _cache = cache;
            _options = options;
            _logger = logger ?? NullLogger<HttpCacheDelegatingHandler>.Instance;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!ShouldCache(request))
                return await base.SendAsync(request, cancellationToken);

            var key = BuildKey(request);
            var cached = await _cache.GetAsync(key, cancellationToken);
            if (cached is { Length: > 0 })
            {
                _logger.LogDebug("RestEase cache hit for {Key}", key);
                return HttpCacheEntry.Deserialize(cached).ToResponse(request);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (!CanStore(response))
                return response;

            var entry = await HttpCacheEntry.FromResponseAsync(response, cancellationToken);
            var ttl = ResolveTtl(response);
            if (ttl <= TimeSpan.Zero)
                return entry.ToResponse(request);

            await _cache.SetAsync(key, entry.Serialize(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
            _logger.LogDebug("RestEase cache stored {Key} ttl={Ttl}", key, ttl);
            return entry.ToResponse(request);
        }

        private bool ShouldCache(HttpRequestMessage request)
        {
            if (!_options.Enabled) return false;
            if (!_options.Methods.Contains(request.Method.Method)) return false;
            var cacheControl = request.Headers.CacheControl;
            if (cacheControl is { NoStore: true } or { NoCache: true }) return false;
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

            var builder = new StringBuilder();
            builder.Append(request.Method.Method).Append(' ');
            builder.Append(request.RequestUri);
            if (_options.IncludeAuthorizationInKey && request.Headers.Authorization is { } auth)
                builder.Append('|').Append(HashAuth(auth));
            return _options.KeyPrefix + builder;
        }

        private static string HashAuth(AuthenticationHeaderValue auth)
        {
            var raw = auth.Scheme + " " + auth.Parameter;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }
    }
}
