using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;

namespace Dosaic.Extensions.RestEase
{
    public class RestEaseClientOptions
    {
        public string BaseAddress { get; set; }
        public TimeSpan? Timeout { get; set; }
        public string UserAgent { get; set; }
        public AuthenticationConfig Authentication { get; set; }
        public JsonSerializerOptions JsonOptions { get; set; }
        public Dictionary<string, string> DefaultHeaders { get; } = new();
    }
}
