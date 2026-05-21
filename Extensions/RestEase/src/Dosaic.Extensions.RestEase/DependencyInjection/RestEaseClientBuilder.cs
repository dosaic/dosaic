using System.Net.Http;
using System.Text.Json;
using System.Threading.RateLimiting;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Caching;
using Dosaic.Extensions.RestEase.RateLimiting;
using Dosaic.Extensions.RestEase.Resilience;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Dosaic.Extensions.RestEase.DependencyInjection
{
    internal sealed class RestEaseClientBuilder : IRestEaseClientBuilder
    {
        public string Name { get; }
        public IServiceCollection Services { get; }
        public IHttpClientBuilder HttpClientBuilder { get; }

        private int _customPipelineCounter;

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

        public IRestEaseClientBuilder AddHandler<THandler>() where THandler : DelegatingHandler
        {
            Services.TryAddTransient<THandler>();
            HttpClientBuilder.AddHttpMessageHandler<THandler>();
            return this;
        }

        public IRestEaseClientBuilder AddResilience(Action<ResilienceConfig> configure = null)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(o =>
            {
                o.Resilience ??= new ResilienceConfig();
                o.Resilience.Enabled = true;
                configure?.Invoke(o.Resilience);
            });
            return this;
        }

        public IRestEaseClientBuilder AddCaching(Action<HttpCacheOptions> configure = null)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(o =>
            {
                o.Caching ??= new HttpCacheOptions();
                o.Caching.Enabled = true;
                configure?.Invoke(o.Caching);
            });
            return this;
        }

        public IRestEaseClientBuilder AddRateLimits(Action<RateLimitsConfig> configure = null)
        {
            Services.AddOptions<RestEaseClientOptions>(Name).Configure(o =>
            {
                o.RateLimits ??= new RateLimitsConfig();
                o.RateLimits.Enabled = true;
                configure?.Invoke(o.RateLimits);
            });
            return this;
        }

        public IRestEaseClientBuilder AddPolly(Action<ResiliencePipelineBuilder<HttpResponseMessage>, ResilienceHandlerContext> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            Services.AddDistributedMemoryCache();
            var slot = Interlocked.Increment(ref _customPipelineCounter);
            HttpClientBuilder.AddResilienceHandler($"{Name}-pipeline-custom-{slot}", configure);
            return this;
        }

        internal void MountAutoPipeline()
        {
            Services.AddDistributedMemoryCache();
            HttpClientBuilder.AddResilienceHandler($"{Name}-pipeline", (pipeline, ctx) =>
            {
                var opts = ctx.ServiceProvider.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(Name);
                BuildPipeline(pipeline, opts, ctx.ServiceProvider);
            });
        }

        internal static void BuildPipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, RestEaseClientOptions opts, IServiceProvider sp)
        {
            if (opts.Caching?.Enabled == true)
            {
                var cache = sp.GetRequiredService<IDistributedCache>();
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Dosaic.RestEase.HttpCache");
                pipeline.AddHttpCache(cache, opts.Caching, logger);
            }

            if (opts.RateLimits is { Enabled: true } rl)
            {
                if (rl.SlidingWindow?.Enabled == true)
                    pipeline.AddRateLimiter(RateLimiterBuilders.Sliding(rl.SlidingWindow));
                if (rl.FixedWindow?.Enabled == true)
                    pipeline.AddRateLimiter(RateLimiterBuilders.Fixed(rl.FixedWindow));
                if (rl.TokenBucket?.Enabled == true)
                    pipeline.AddRateLimiter(RateLimiterBuilders.TokenBucket(rl.TokenBucket));
                if (rl.Concurrency?.Enabled == true)
                    pipeline.AddRateLimiter(RateLimiterBuilders.Concurrency(rl.Concurrency));
            }

            if (opts.Resilience is { Enabled: true } r)
            {
                if (r.TotalRequestTimeout is { } total)
                    pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = total, Name = "TotalTimeout" });

                var retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = r.MaxRetryAttempts ?? 3,
                    Delay = r.BaseDelay ?? TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                };

                if (r.AdditionalRetryStatusCodes is { Count: > 0 } codes)
                {
                    var defaultShouldHandle = retry.ShouldHandle;
                    retry.ShouldHandle = async args =>
                        (args.Outcome.Result is { } resp && codes.Contains(resp.StatusCode))
                        || await defaultShouldHandle(args);
                }

                r.ConfigureRetry?.Invoke(retry);
                pipeline.AddRetry(retry);

                if (r.AttemptTimeout is { } attempt)
                    pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = attempt, Name = "AttemptTimeout" });
            }
        }

        private static class RateLimiterBuilders
        {
            public static RateLimiterStrategyOptions Concurrency(ConcurrencyLimiterConfig c)
            {
                var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = c.PermitLimit,
                    QueueLimit = c.QueueLimit,
                    QueueProcessingOrder = c.QueueProcessingOrder
                });
                return new RateLimiterStrategyOptions { Name = "Concurrency", RateLimiter = args => limiter.AcquireAsync(1, args.Context.CancellationToken) };
            }

            public static RateLimiterStrategyOptions Sliding(SlidingWindowLimiterConfig c)
            {
                var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = c.PermitLimit,
                    QueueLimit = c.QueueLimit,
                    QueueProcessingOrder = c.QueueProcessingOrder,
                    Window = c.Window,
                    SegmentsPerWindow = c.SegmentsPerWindow,
                    AutoReplenishment = c.AutoReplenishment
                });
                return new RateLimiterStrategyOptions { Name = "SlidingWindow", RateLimiter = args => limiter.AcquireAsync(1, args.Context.CancellationToken) };
            }

            public static RateLimiterStrategyOptions Fixed(FixedWindowLimiterConfig c)
            {
                var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    PermitLimit = c.PermitLimit,
                    QueueLimit = c.QueueLimit,
                    QueueProcessingOrder = c.QueueProcessingOrder,
                    Window = c.Window,
                    AutoReplenishment = c.AutoReplenishment
                });
                return new RateLimiterStrategyOptions { Name = "FixedWindow", RateLimiter = args => limiter.AcquireAsync(1, args.Context.CancellationToken) };
            }

            public static RateLimiterStrategyOptions TokenBucket(TokenBucketLimiterConfig c)
            {
                var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = c.PermitLimit,
                    QueueLimit = c.QueueLimit,
                    QueueProcessingOrder = c.QueueProcessingOrder,
                    TokensPerPeriod = c.TokensPerPeriod,
                    ReplenishmentPeriod = c.ReplenishmentPeriod,
                    AutoReplenishment = c.AutoReplenishment
                });
                return new RateLimiterStrategyOptions { Name = "TokenBucket", RateLimiter = args => limiter.AcquireAsync(1, args.Context.CancellationToken) };
            }
        }
    }
}
