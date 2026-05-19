using System.Net.Http;
using System.Text;
using System.Text.Json;
using RestEase;

namespace Dosaic.Extensions.RestEase.Json
{
    internal sealed class SystemTextJsonRequestBodySerializer : RequestBodySerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonRequestBodySerializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        public override HttpContent SerializeBody<T>(T body, RequestBodySerializerInfo info)
        {
            if (body is null) return null;
            var json = JsonSerializer.Serialize(body, _options);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }
    }
}
