using System.Net.Http;
using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Handlers;
using Dosaic.Extensions.RestEase.Json;
using Polly;
using RestEase;

namespace Dosaic.Extensions.RestEase
{
    public static class RestClientFactory
    {
        public static T Create<T>(string baseAddress) => Create<T>(baseAddress, _ => { });

        public static T Create<T>(string baseAddress, AuthenticationConfig authenticationConfig) =>
            Create<T>(baseAddress, o => o.Authentication = authenticationConfig);

        public static T Create<T>(string baseAddress, ResiliencePipeline<HttpResponseMessage> pipeline) =>
            Create<T>(baseAddress, o => o.ResiliencePipeline = pipeline);

        public static T Create<T>(string baseAddress, AuthenticationConfig authenticationConfig, ResiliencePipeline<HttpResponseMessage> pipeline) =>
            Create<T>(baseAddress, o =>
            {
                o.Authentication = authenticationConfig;
                o.ResiliencePipeline = pipeline;
            });

        public static T Create<T>(string baseAddress, Action<StandaloneClientOptions> configure)
        {
            var options = new StandaloneClientOptions { BaseAddress = baseAddress };
            configure?.Invoke(options);

            var jsonOptions = options.JsonOptions ?? RestEaseDefaults.CreateDefaultJsonOptions();
            var pipeline = options.ResiliencePipeline ?? RestEaseDefaults.CreateDefaultPipeline();

            HttpMessageHandler handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };

            if (options.Authentication is { Enabled: true })
            {
                var tokenProvider = OAuth2TokenProvider.Create(options.Authentication, jsonOptions);
                handler = new OAuth2DelegatingHandler(tokenProvider) { InnerHandler = handler };
            }

            handler = new ResilienceDelegatingHandler(pipeline) { InnerHandler = handler };

            if (!string.IsNullOrWhiteSpace(options.UserAgent))
                handler = new UserAgentHandler(options.UserAgent) { InnerHandler = handler };

            var httpClient = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri(options.BaseAddress) };
            if (options.Timeout is { } timeout) httpClient.Timeout = timeout;
            foreach (var (k, v) in options.DefaultHeaders)
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(k, v);

            var restClient = new RestClient(httpClient)
            {
                RequestBodySerializer = new SystemTextJsonRequestBodySerializer(jsonOptions),
                ResponseDeserializer = new SystemTextJsonResponseDeserializer(jsonOptions)
            };
            return restClient.For<T>();
        }

        public class StandaloneClientOptions
        {
            public string BaseAddress { get; set; }
            public TimeSpan? Timeout { get; set; }
            public string UserAgent { get; set; }
            public AuthenticationConfig Authentication { get; set; }
            public JsonSerializerOptions JsonOptions { get; set; }
            public ResiliencePipeline<HttpResponseMessage> ResiliencePipeline { get; set; }
            public Dictionary<string, string> DefaultHeaders { get; } = new();
        }
    }
}
