using System.Net.Http;
using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Dosaic.Extensions.RestEase.DependencyInjection
{
    public interface IRestEaseClientBuilder
    {
        string Name { get; }
        IServiceCollection Services { get; }
        IHttpClientBuilder HttpClientBuilder { get; }

        IRestEaseClientBuilder ConfigureOptions(Action<RestEaseClientOptions> configure);
        IRestEaseClientBuilder ConfigureJson(Action<JsonSerializerOptions> configure);
        IRestEaseClientBuilder ConfigureHttpClient(Action<HttpClient> configure);
        IRestEaseClientBuilder AddOAuth2(Action<AuthenticationConfig> configure);
        IRestEaseClientBuilder AddTokenProvider<TProvider>() where TProvider : class, ITokenProvider;
        IRestEaseClientBuilder AddStandardResilience();
        IRestEaseClientBuilder AddResilience(ResiliencePipeline<HttpResponseMessage> pipeline);
        IRestEaseClientBuilder AddHandler<THandler>() where THandler : DelegatingHandler;
    }
}
