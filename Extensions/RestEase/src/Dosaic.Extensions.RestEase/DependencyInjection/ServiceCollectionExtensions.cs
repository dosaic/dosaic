using System.Net.Http;
using Dosaic.Extensions.RestEase.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RestEase;

namespace Dosaic.Extensions.RestEase.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IRestEaseClientBuilder AddDosaicRestClient<TApi>(this IServiceCollection services, Action<RestEaseClientOptions> configure = null)
            where TApi : class
            => services.AddDosaicRestClient<TApi>(typeof(TApi).FullName, configure);

        public static IRestEaseClientBuilder AddDosaicRestClient<TApi>(this IServiceCollection services, string name, Action<RestEaseClientOptions> configure = null)
            where TApi : class
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Client name must be provided.", nameof(name));

            var optionsBuilder = services.AddOptions<RestEaseClientOptions>(name);
            if (configure != null) optionsBuilder.Configure(configure);

            var httpClientBuilder = services.AddHttpClient(name).ConfigureHttpClient((sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(name);
                if (!string.IsNullOrWhiteSpace(opts.BaseAddress))
                    http.BaseAddress = new Uri(opts.BaseAddress);
                if (opts.Timeout is { } timeout)
                    http.Timeout = timeout;
                if (!string.IsNullOrWhiteSpace(opts.UserAgent))
                    http.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
                foreach (var (k, v) in opts.DefaultHeaders)
                    http.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
            });

            services.TryAddTransient<TApi>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(name);
                var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(name);
                var jsonOptions = opts.JsonOptions ?? RestEaseDefaults.CreateDefaultJsonOptions();
                var restClient = new RestClient(http)
                {
                    RequestBodySerializer = new SystemTextJsonRequestBodySerializer(jsonOptions),
                    ResponseDeserializer = new SystemTextJsonResponseDeserializer(jsonOptions)
                };
                return restClient.For<TApi>();
            });

            services.TryAddSingleton<IRestClientFactory, DefaultRestClientFactory>();

            return new RestEaseClientBuilder(name, services, httpClientBuilder);
        }
    }

    internal sealed class DefaultRestClientFactory : IRestClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public DefaultRestClientFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
        public T Create<T>(string name = null) => _serviceProvider.GetRequiredService<T>();
    }
}
