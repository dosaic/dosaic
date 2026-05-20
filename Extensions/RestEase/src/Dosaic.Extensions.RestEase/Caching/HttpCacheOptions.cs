using System.Net.Http;

namespace Dosaic.Extensions.RestEase.Caching
{
    public sealed class HttpCacheOptions
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan? MaxTtl { get; set; }
        public HashSet<string> Methods { get; set; } = new(StringComparer.OrdinalIgnoreCase) { HttpMethod.Get.Method };
        public HashSet<int> CacheableStatusCodes { get; set; } = new() { 200, 203, 300, 301, 404, 410 };
        public bool RespectCacheControl { get; set; } = true;
        public bool IncludeAuthorizationInKey { get; set; }
        public string KeyPrefix { get; set; } = "dosaic:restease:";
        public Func<HttpRequestMessage, string> KeyBuilder { get; set; }
        public Func<HttpRequestMessage, bool> ShouldCacheRequest { get; set; }
        public Func<HttpResponseMessage, bool> ShouldCacheResponse { get; set; }
    }
}
