using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Caching;
using Dosaic.Extensions.RestEase.RateLimiting;
using Dosaic.Extensions.RestEase.Resilience;

namespace Dosaic.Extensions.RestEase
{
    public class RestEaseClientOptions
    {
        public string BaseAddress { get; set; }
        public TimeSpan? Timeout { get; set; }
        public string UserAgent { get; set; }
        public AuthenticationConfig Authentication { get; set; } = new AuthenticationConfig() { Enabled = false };
        public HttpCacheOptions Caching { get; set; }
        public ResilienceConfig Resilience { get; set; }
        public RateLimitsConfig RateLimits { get; set; }
        public JsonSerializerOptions JsonOptions { get; set; }
        public Dictionary<string, string> DefaultHeaders { get; } = new();
    }
}
