using System.Net.Http;
using System.Text.Json;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Caching;
using Dosaic.Extensions.RestEase.Handlers;
using Dosaic.Extensions.RestEase.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
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

        public IRestEaseClientBuilder AddStandardResilience(Action<HttpStandardResilienceOptions> configure = null)
        {
            var resilienceBuilder = HttpClientBuilder.AddStandardResilienceHandler();
            if (configure is not null) resilienceBuilder.Configure(configure);
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

        public IRestEaseClientBuilder AddRateLimits(Action<RateLimitsConfig> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            var config = new RateLimitsConfig();
            configure(config);
            ApplyRateLimits(config);
            return this;
        }

        internal void ApplyRateLimits(RateLimitsConfig config)
        {
            if (config is null || !config.Enabled) return;

            if (config.SlidingWindow?.Enabled == true)
            {
                var limiter = RateLimitDelegatingHandler.BuildSlidingWindow(config.SlidingWindow);
                HttpClientBuilder.AddHttpMessageHandler(() => new RateLimitDelegatingHandler(limiter, config.ThrowOnRejection));
            }
            if (config.FixedWindow?.Enabled == true)
            {
                var limiter = RateLimitDelegatingHandler.BuildFixedWindow(config.FixedWindow);
                HttpClientBuilder.AddHttpMessageHandler(() => new RateLimitDelegatingHandler(limiter, config.ThrowOnRejection));
            }
            if (config.TokenBucket?.Enabled == true)
            {
                var limiter = RateLimitDelegatingHandler.BuildTokenBucket(config.TokenBucket);
                HttpClientBuilder.AddHttpMessageHandler(() => new RateLimitDelegatingHandler(limiter, config.ThrowOnRejection));
            }
            if (config.Concurrency?.Enabled == true)
            {
                var limiter = RateLimitDelegatingHandler.BuildConcurrency(config.Concurrency);
                HttpClientBuilder.AddHttpMessageHandler(() => new RateLimitDelegatingHandler(limiter, config.ThrowOnRejection));
            }
        }

        public IRestEaseClientBuilder AddCaching(Action<HttpCacheOptions> configure = null)
        {
            Services.AddOptions<HttpCacheOptions>(Name)
                .Configure<IOptionsMonitor<RestEaseClientOptions>>((target, monitor) =>
                {
                    var src = monitor.Get(Name).Caching;
                    if (src is not null) CopyCachingTo(src, target);
                });
            if (configure is not null) Services.AddOptions<HttpCacheOptions>(Name).Configure(configure);

            Services.AddDistributedMemoryCache();

            HttpClientBuilder.AddHttpMessageHandler(sp =>
            {
                var cache = sp.GetRequiredService<IDistributedCache>();
                var opts = sp.GetRequiredService<IOptionsMonitor<HttpCacheOptions>>().Get(Name);
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<HttpCacheDelegatingHandler>();
                return new HttpCacheDelegatingHandler(cache, opts, logger);
            });
            return this;
        }

        private static void CopyCachingTo(HttpCacheOptions source, HttpCacheOptions target)
        {
            target.Enabled = source.Enabled;
            target.DefaultTtl = source.DefaultTtl;
            target.MaxTtl = source.MaxTtl;
            target.RespectCacheControl = source.RespectCacheControl;
            target.IncludeAuthorizationInKey = source.IncludeAuthorizationInKey;
            target.KeyPrefix = source.KeyPrefix;
            target.KeyBuilder = source.KeyBuilder;
            target.ShouldCacheRequest = source.ShouldCacheRequest;
            target.ShouldCacheResponse = source.ShouldCacheResponse;
            if (source.Methods is { Count: > 0 })
                target.Methods = new HashSet<string>(source.Methods, StringComparer.OrdinalIgnoreCase);
            if (source.CacheableStatusCodes is { Count: > 0 })
                target.CacheableStatusCodes = new HashSet<int>(source.CacheableStatusCodes);
        }
    }
}
