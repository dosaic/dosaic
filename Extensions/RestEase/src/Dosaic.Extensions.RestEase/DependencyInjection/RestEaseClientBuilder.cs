using System.Net.Http;
using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;

namespace Dosaic.Extensions.RestEase.DependencyInjection
{
    internal sealed class RestEaseClientBuilder : IRestEaseClientBuilder
    {
        public string Name { get; }
        public IServiceCollection Services { get; }
        public IHttpClientBuilder HttpClientBuilder { get; }

        public RestEaseClientBuilder(string name, IServiceCollection services, IHttpClientBuilder httpClientBuilder)
        {
            Name = name;
            Services = services;
            HttpClientBuilder = httpClientBuilder;
        }

        public IRestEaseClientBuilder ConfigureOptions(Action<RestEaseClientOptions> configure)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(configure);
            return this;
        }

        public IRestEaseClientBuilder ConfigureJson(Action<JsonSerializerOptions> configure)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(o =>
            {
                o.JsonOptions ??= RestEaseDefaults.CreateDefaultJsonOptions();
                configure(o.JsonOptions);
            });
            return this;
        }

        public IRestEaseClientBuilder ConfigureHttpClient(Action<HttpClient> configure)
        {
            HttpClientBuilder.ConfigureHttpClient(configure);
            return this;
        }

        public IRestEaseClientBuilder AddOAuth2(Action<AuthenticationConfig> configure)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(o =>
            {
                o.Authentication ??= new AuthenticationConfig();
                o.Authentication.Enabled = true;
                configure(o.Authentication);
            });
            Services.AddKeyedSingleton<ITokenProvider>(Name, (sp, key) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get((string)key);
                var jsonOptions = opts.JsonOptions ?? RestEaseDefaults.CreateDefaultJsonOptions();
                return OAuth2TokenProvider.Create(opts.Authentication, jsonOptions);
            });
            HttpClientBuilder.AddHttpMessageHandler(sp =>
                new OAuth2DelegatingHandler(sp.GetRequiredKeyedService<ITokenProvider>(Name)));
            return this;
        }

        public IRestEaseClientBuilder AddTokenProvider<TProvider>() where TProvider : class, ITokenProvider
        {
            Services.AddKeyedSingleton<ITokenProvider, TProvider>(Name);
            HttpClientBuilder.AddHttpMessageHandler(sp =>
                new OAuth2DelegatingHandler(sp.GetRequiredKeyedService<ITokenProvider>(Name)));
            return this;
        }

        public IRestEaseClientBuilder AddStandardResilience()
        {
            HttpClientBuilder.AddStandardResilienceHandler();
            return this;
        }

        public IRestEaseClientBuilder AddResilience(ResiliencePipeline<HttpResponseMessage> pipeline)
        {
            HttpClientBuilder.AddHttpMessageHandler(() => new ResilienceDelegatingHandler(pipeline));
            return this;
        }

        public IRestEaseClientBuilder AddHandler<THandler>() where THandler : DelegatingHandler
        {
            Services.TryAddTransient<THandler>();
            HttpClientBuilder.AddHttpMessageHandler<THandler>();
            return this;
        }
    }
}
