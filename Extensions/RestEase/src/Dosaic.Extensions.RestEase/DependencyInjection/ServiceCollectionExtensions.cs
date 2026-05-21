using System.Net.Http;
using Dosaic.Extensions.RestEase.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RestEase;

namespace Dosaic.Extensions.RestEase.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IRestEaseClientBuilder AddRestEaseApi<TApi>(this IServiceCollection services, Action<RestEaseClientOptions> configure = null)
            where TApi : class
            => services.AddRestEaseApi<TApi>(typeof(TApi).FullName, configure);

        public static IRestEaseClientBuilder AddRestEaseApi<TApi>(this IServiceCollection services, RestEaseClientOptions options)
            where TApi : class
            => services.AddRestEaseApi<TApi>(typeof(TApi).FullName, options);

        public static IRestEaseClientBuilder AddRestEaseApi<TApi>(this IServiceCollection services, string name, RestEaseClientOptions options)
            where TApi : class
        {
            ArgumentNullException.ThrowIfNull(options);
            return services.AddRestEaseApi<TApi>(name, target => ApplyTo(options, target));
        }

        public static IRestEaseClientBuilder AddRestEaseApiFromConfiguration<TApi>(this IServiceCollection services, IConfiguration configuration, string sectionKey)
            where TApi : class
            => services.AddRestEaseApiFromConfiguration<TApi>(typeof(TApi).FullName, configuration, sectionKey);

        public static IRestEaseClientBuilder AddRestEaseApiFromConfiguration<TApi>(this IServiceCollection services, string name, IConfiguration configuration, string sectionKey)
            where TApi : class
        {
            ArgumentNullException.ThrowIfNull(configuration);
            if (string.IsNullOrWhiteSpace(sectionKey)) throw new ArgumentException("Configuration section key must be provided.", nameof(sectionKey));
            return services.AddRestEaseApiFromConfiguration<TApi>(name, configuration.GetSection(sectionKey));
        }

        public static IRestEaseClientBuilder AddRestEaseApiFromConfiguration<TApi>(this IServiceCollection services, IConfigurationSection section)
            where TApi : class
            => services.AddRestEaseApiFromConfiguration<TApi>(typeof(TApi).FullName, section);

        public static IRestEaseClientBuilder AddRestEaseApiFromConfiguration<TApi>(this IServiceCollection services, string name, IConfigurationSection section)
            where TApi : class
        {
            ArgumentNullException.ThrowIfNull(section);
            return services.AddRestEaseApi<TApi>(name, target => section.Bind(target));
        }

        internal static void ApplyTo(RestEaseClientOptions source, RestEaseClientOptions target)
        {
            target.BaseAddress = source.BaseAddress;
            target.Timeout = source.Timeout;
            target.UserAgent = source.UserAgent;
            target.Authentication = source.Authentication;
            target.Caching = source.Caching;
            target.Resilience = source.Resilience;
            target.RateLimits = source.RateLimits;
            target.JsonOptions = source.JsonOptions;
            foreach (var (k, v) in source.DefaultHeaders)
                target.DefaultHeaders[k] = v;
        }

        public static IRestEaseClientBuilder AddRestEaseApi<TApi>(this IServiceCollection services, string name, Action<RestEaseClientOptions> configure = null)
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

            var builder = new RestEaseClientBuilder(name, services, httpClientBuilder);
            builder.MountAutoPipeline();
            return builder;
        }
    }

    internal sealed class DefaultRestClientFactory : IRestClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public DefaultRestClientFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
        public T Create<T>(string name = null) => _serviceProvider.GetRequiredService<T>();
    }
}
