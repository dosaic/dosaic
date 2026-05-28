using System.Net.Http;
using System.Text.Json;
using RestEase;

namespace Dosaic.Extensions.RestEase.Json
{
    internal sealed class SystemTextJsonResponseDeserializer : ResponseDeserializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonResponseDeserializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        public override T Deserialize<T>(string content, HttpResponseMessage response, ResponseDeserializerInfo info)
        {
            if (string.IsNullOrEmpty(content)) return default;
            return JsonSerializer.Deserialize<T>(content, _options);
        }
    }
}
