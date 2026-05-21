using System.Net.Http;
using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Caching;
using Dosaic.Extensions.RestEase.RateLimiting;
using Dosaic.Extensions.RestEase.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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
        IRestEaseClientBuilder AddHandler<THandler>() where THandler : DelegatingHandler;
        IRestEaseClientBuilder AddResilience(Action<ResilienceConfig> configure = null);
        IRestEaseClientBuilder AddCaching(Action<HttpCacheOptions> configure = null);
        IRestEaseClientBuilder AddRateLimits(Action<RateLimitsConfig> configure = null);
        IRestEaseClientBuilder AddPolly(Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext> configure);
    }
}
