using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dosaic.Extensions.RestEase.Caching
{
    internal sealed class HttpCacheEntry
    {
        [JsonPropertyName("s")] public int StatusCode { get; set; }
        [JsonPropertyName("h")] public Dictionary<string, string[]> Headers { get; set; } = new();
        [JsonPropertyName("ch")] public Dictionary<string, string[]> ContentHeaders { get; set; } = new();
        [JsonPropertyName("b")] public byte[] Body { get; set; } = [];

        public static async Task<HttpCacheEntry> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var entry = new HttpCacheEntry
            {
                StatusCode = (int)response.StatusCode,
                Body = response.Content is null ? [] : await response.Content.ReadAsByteArrayAsync(ct)
            };
            foreach (var header in response.Headers)
                entry.Headers[header.Key] = header.Value.ToArray();
            if (response.Content is not null)
                foreach (var header in response.Content.Headers)
                    entry.ContentHeaders[header.Key] = header.Value.ToArray();
            return entry;
        }

        public HttpResponseMessage ToResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage((HttpStatusCode)StatusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(Body)
            };
            foreach (var (k, v) in Headers)
                response.Headers.TryAddWithoutValidation(k, v);
            response.Content.Headers.Clear();
            foreach (var (k, v) in ContentHeaders)
                response.Content.Headers.TryAddWithoutValidation(k, v);
            return response;
        }

        public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this);

        public static HttpCacheEntry Deserialize(byte[] bytes) => JsonSerializer.Deserialize<HttpCacheEntry>(bytes);
    }
}
