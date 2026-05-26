using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.Caching;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Dosaic.Extensions.RestEase.Handlers;
using Dosaic.Extensions.RestEase.RateLimiting;
using Dosaic.Extensions.RestEase.Resilience;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Polly.RateLimiting;
using Polly.Timeout;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class CoverageGapTests
    {
        private WireMockServer _server;
        private static string ClientName => typeof(ISomeApi).FullName!;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        private static HttpClient ResolveHttpClient(IServiceProvider sp) =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        // ---------- RateLimitsConfig defaults ----------

        [Test]
        public void DefaultsAreCorrectForFixedAndTokenBucketLimiters()
        {
            var fixedWindow = new FixedWindowLimiterConfig();
            fixedWindow.Window.Should().Be(TimeSpan.FromSeconds(1));
            fixedWindow.AutoReplenishment.Should().BeTrue();

            var bucket = new TokenBucketLimiterConfig();
            bucket.TokensPerPeriod.Should().Be(10);
            bucket.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(1));
            bucket.AutoReplenishment.Should().BeTrue();
        }

        // ---------- RestEaseClientBuilder pipeline branches ----------

        [Test]
        public async Task BuildPipelineRateLimitFallbackPopulatesRetryAfterHeader()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.RateLimits = new RateLimitsConfig
                {
                    Enabled = true,
                    ThrowOnRejection = false,
                    FixedWindow = new FixedWindowLimiterConfig
                    {
                        Enabled = true,
                        PermitLimit = 1,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(30),
                        AutoReplenishment = true
                    }
                };
            });

            await using var sp = services.BuildServiceProvider();
            var http = ResolveHttpClient(sp);

            var ok = await http.GetAsync("/");
            ok.StatusCode.Should().Be(HttpStatusCode.OK);

            var rejected = await http.GetAsync("/");
            rejected.StatusCode.Should().Be((HttpStatusCode)429);
            rejected.Headers.RetryAfter.Should().NotBeNull();
        }

        [Test]
        public async Task BuildPipelineRegistersFixedWindowLimiterWhenEnabled()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.RateLimits = new RateLimitsConfig
                {
                    Enabled = true,
                    ThrowOnRejection = true,
                    FixedWindow = new FixedWindowLimiterConfig
                    {
                        Enabled = true,
                        PermitLimit = 1,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(30)
                    }
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            Func<Task> second = () => client.Get(CancellationToken.None);
            await second.Should().ThrowAsync<RateLimiterRejectedException>();
        }

        [Test]
        public async Task BuildPipelineRegistersTokenBucketLimiterWhenEnabled()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.RateLimits = new RateLimitsConfig
                {
                    Enabled = true,
                    ThrowOnRejection = true,
                    TokenBucket = new TokenBucketLimiterConfig
                    {
                        Enabled = true,
                        PermitLimit = 1,
                        QueueLimit = 0,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(5),
                        AutoReplenishment = false
                    }
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            Func<Task> second = () => client.Get(CancellationToken.None);
            await second.Should().ThrowAsync<RateLimiterRejectedException>();
        }

        [Test]
        public async Task BuildPipelineRegistersTotalRequestTimeoutWhenConfigured()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]").WithDelay(TimeSpan.FromMilliseconds(500)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Resilience = new ResilienceConfig
                {
                    Enabled = true,
                    MaxRetryAttempts = 1,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    TotalRequestTimeout = TimeSpan.FromMilliseconds(50)
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            Func<Task> act = () => client.Get(CancellationToken.None);
            await act.Should().ThrowAsync<Exception>();
        }

        [Test]
        public async Task BuildPipelineRegistersAttemptTimeoutWhenConfigured()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]").WithDelay(TimeSpan.FromMilliseconds(500)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Resilience = new ResilienceConfig
                {
                    Enabled = true,
                    MaxRetryAttempts = 1,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    AttemptTimeout = TimeSpan.FromMilliseconds(50)
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            Func<Task> act = () => client.Get(CancellationToken.None);
            (await act.Should().ThrowAsync<Exception>()).Which.Should().BeAssignableTo<Exception>();
        }

        // ---------- Builder ??= retain-existing branches ----------

        [Test]
        public async Task ConfigureJsonRetainsExistingJsonOptions()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/")
                .ConfigureJson(j => j.WriteIndented = true)
                .ConfigureJson(j => j.PropertyNameCaseInsensitive = false);

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.JsonOptions!.WriteIndented.Should().BeTrue();
            opts.JsonOptions.PropertyNameCaseInsensitive.Should().BeFalse();
        }

        [Test]
        public async Task AddOAuth2RetainsExistingAuthentication()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
                {
                    o.BaseAddress = "http://localhost/";
                    o.Authentication = new AuthenticationConfig { Enabled = false, ClientId = "seed" };
                })
                .AddOAuth2(a => a.ClientSecret = "added");

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.Authentication!.ClientId.Should().Be("seed");
            opts.Authentication.ClientSecret.Should().Be("added");
            opts.Authentication.Enabled.Should().BeTrue();
        }

        [Test]
        public async Task AddResilienceRetainsExisting()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
                {
                    o.BaseAddress = "http://localhost/";
                    o.Resilience = new ResilienceConfig { MaxRetryAttempts = 7 };
                })
                .AddResilience(r => r.BaseDelay = TimeSpan.FromSeconds(2));

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.Resilience!.MaxRetryAttempts.Should().Be(7);
            opts.Resilience.BaseDelay.Should().Be(TimeSpan.FromSeconds(2));
        }

        [Test]
        public async Task AddCachingRetainsExisting()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
                {
                    o.BaseAddress = "http://localhost/";
                    o.Caching = new HttpCacheOptions { KeyPrefix = "seed:" };
                })
                .AddCaching(c => c.DefaultTtl = TimeSpan.FromMinutes(7));

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.Caching!.KeyPrefix.Should().Be("seed:");
            opts.Caching.DefaultTtl.Should().Be(TimeSpan.FromMinutes(7));
        }

        [Test]
        public async Task AddRateLimitsRetainsExisting()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
                {
                    o.BaseAddress = "http://localhost/";
                    o.RateLimits = new RateLimitsConfig { ThrowOnRejection = true };
                })
                .AddRateLimits(r => r.Concurrency = new ConcurrencyLimiterConfig { PermitLimit = 5 });

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.RateLimits!.ThrowOnRejection.Should().BeTrue();
            opts.RateLimits.Concurrency!.PermitLimit.Should().Be(5);
        }

        [Test]
        public async Task AddRateLimitsNullConfigureDelegateDoesNotThrow()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = "http://localhost/")
                .AddRateLimits();

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.RateLimits!.Enabled.Should().BeTrue();
        }

        // ---------- ServiceCollectionExtensions ----------

        [Test]
        public async Task AddRestEaseApiFromConfigurationWithSectionOnlyUsesTypeFullNameAsClientName()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Api:BaseAddress"] = _server.Url
                })
                .Build();

            var services = new ServiceCollection();
            services.AddRestEaseApiFromConfiguration<ISomeApi>(configuration.GetSection("Api"));

            await using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptionsMonitor<RestEaseClientOptions>>().Get(ClientName);
            opts.BaseAddress.Should().Be(_server.Url);

            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        [Test]
        public async Task DefaultRestClientFactoryCreateWithExplicitNameBuildsRestClientUsingDefaults()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "x" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>("named-client", o => o.BaseAddress = _server.Url);

            await using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IRestClientFactory>();
            var api = factory.Create<ISomeApi>("named-client");
            var result = await api.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public async Task DefaultRestClientFactoryCreateWithExplicitNameBuildsRestClientUsingProvidedJsonOptions()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "y" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>("named-client-2", o =>
            {
                o.BaseAddress = _server.Url;
                o.JsonOptions = RestEaseDefaults.CreateDefaultJsonOptions();
            });

            await using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IRestClientFactory>();
            var api = factory.Create<ISomeApi>("named-client-2");
            var result = await api.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public async Task AddRestEaseApiWithoutBaseAddressDoesNotSetUri()
        {
            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = "   ";
            });

            await using var sp = services.BuildServiceProvider();
            var http = ResolveHttpClient(sp);
            http.BaseAddress.Should().BeNull();
        }

        // ---------- HttpCacheResilienceStrategy ----------

        [Test]
        public async Task CacheRespectsResponseMaxAgeOverridesDefaultTtl()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create()
                    .WithSuccess()
                    .WithHeader("Cache-Control", "max-age=60")
                    .WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions { DefaultTtl = TimeSpan.FromMilliseconds(10) };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await Task.Delay(150);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        [Test]
        public async Task CacheClampsTtlToMaxTtl()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create()
                    .WithSuccess()
                    .WithHeader("Cache-Control", "max-age=60")
                    .WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions
                {
                    DefaultTtl = TimeSpan.FromMinutes(5),
                    MaxTtl = TimeSpan.FromMilliseconds(50)
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await Task.Delay(250);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheKeyIncludesHashedAuthorizationWhenConfigured()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions
                {
                    DefaultTtl = TimeSpan.FromMinutes(1),
                    IncludeAuthorizationInKey = true
                };
            });

            await using var sp = services.BuildServiceProvider();
            var http = ResolveHttpClient(sp);

            async Task SendAsync(string token)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var resp = await http.SendAsync(req);
                resp.IsSuccessStatusCode.Should().BeTrue();
            }

            await SendAsync("A");
            await SendAsync("B");
            await SendAsync("A");
            await SendAsync("B");

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheDisabledOptionsBypassesCache()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions { Enabled = false };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();

            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheNoStoreRequestBypasses()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions { DefaultTtl = TimeSpan.FromMinutes(5) };
            });

            await using var sp = services.BuildServiceProvider();
            var http = ResolveHttpClient(sp);

            async Task SendNoStoreAsync()
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/");
                req.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                using var _ = await http.SendAsync(req);
            }

            await SendNoStoreAsync();
            await SendNoStoreAsync();

            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheNoStoreResponseNotStored()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess()
                    .WithHeader("Cache-Control", "no-store")
                    .WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions { DefaultTtl = TimeSpan.FromMinutes(5) };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CachePrivateResponseNotStored()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess()
                    .WithHeader("Cache-Control", "private")
                    .WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions { DefaultTtl = TimeSpan.FromMinutes(5) };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheShouldCacheRequestPredicateFalseBypasses()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions
                {
                    DefaultTtl = TimeSpan.FromMinutes(5),
                    ShouldCacheRequest = _ => false
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheShouldCacheResponsePredicateFalseNotStored()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions
                {
                    DefaultTtl = TimeSpan.FromMinutes(5),
                    ShouldCacheResponse = _ => false
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(2);
        }

        [Test]
        public async Task CacheCustomKeyBuilderUsedInsteadOfDefault()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Caching = new HttpCacheOptions
                {
                    DefaultTtl = TimeSpan.FromMinutes(5),
                    KeyPrefix = "kb:",
                    KeyBuilder = _ => "fixed"
                };
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var cache = sp.GetRequiredService<IDistributedCache>();

            await client.Get(CancellationToken.None);
            (await cache.GetAsync("kb:fixed")).Should().NotBeNull();
            await client.Get(CancellationToken.None);
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        // ---------- HttpCacheEntry round-trip ----------

        [Test]
        public async Task HttpCacheEntryRoundTripsContentAndResponseHeaders()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
            response.Headers.TryAddWithoutValidation("X-Resp", "rv");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var entry = await HttpCacheEntry.FromResponseAsync(response, CancellationToken.None);
            var bytes = entry.Serialize();
            var roundTripEntry = HttpCacheEntry.Deserialize(bytes);

            using var request = new HttpRequestMessage(HttpMethod.Get, "http://x/y");
            using var roundTripped = roundTripEntry.ToResponse(request);

            roundTripped.Headers.GetValues("X-Resp").Should().Contain("rv");
            roundTripped.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            (await roundTripped.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        }

        // ---------- RestEaseDefaults.ComputeRetryDelay ----------

        [Test]
        public void ComputeRetryDelayDateInPastReturnsZero()
        {
            var past = DateTimeOffset.UtcNow.AddSeconds(-5);
            var delay = RestEaseDefaults.ComputeRetryDelay(new RetryConditionHeaderValue(past));
            delay.Should().Be(TimeSpan.Zero);
        }

        [Test]
        public void ComputeRetryDelayDateInFutureReturnsPositive()
        {
            var future = DateTimeOffset.UtcNow.AddSeconds(10);
            var delay = RestEaseDefaults.ComputeRetryDelay(new RetryConditionHeaderValue(future));
            delay.Should().NotBeNull();
            delay!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        }

        // ---------- RestClientFactory.Create with disabled auth ----------

        [Test]
        public async Task CreateWithAuthenticationDisabledDoesNotWrapWithOAuthHandler()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var client = RestClientFactory.Create<ISomeApi>(_server.Url, new AuthenticationConfig { Enabled = false });
            await client.Get(CancellationToken.None);

            _server.FindLogEntries(Request.Create().WithPath("/auth/token").UsingPost()).Should().BeEmpty();
            _server.FindLogEntries(Request.Create().WithPath("/").UsingGet()).Should().HaveCount(1);
        }

        // ---------- OAuth2TokenProvider empty client id/secret ----------

        [Test]
        public async Task GetTokenWithEmptyClientIdAndSecretOmitsThemFromFormBodyOnInitialAndRefresh()
        {
            var path = "/auth/token";
            _server.Given(Request.Create().WithPath(path).WithBody((string b) => b.Contains("grant_type=password")).UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new
                {
                    token_type = "Bearer",
                    access_token = "a",
                    expires_in = 1,
                    refresh_expires_in = 600,
                    refresh_token = "r"
                }));
            _server.Given(Request.Create().WithPath(path).WithBody((string b) => b.Contains("grant_type=refresh_token")).UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new
                {
                    token_type = "Bearer",
                    access_token = "a2",
                    expires_in = 600,
                    refresh_expires_in = 600,
                    refresh_token = "r"
                }));

            var config = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = path,
                GrantType = GrantType.Password,
                ClientId = "",
                ClientSecret = "",
                Username = "u",
                Password = "p",
                RefreshSkew = TimeSpan.Zero
            };

            var provider = OAuth2TokenProvider.Create(config);
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("a");
            await Task.Delay(1100);
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("a2");

            var posts = _server.FindLogEntries(Request.Create().WithPath(path).UsingPost()).ToList();
            posts.Should().HaveCount(2);
            foreach (var entry in posts)
            {
                entry.RequestMessage.Body.Should().NotContain("client_id=");
                entry.RequestMessage.Body.Should().NotContain("client_secret=");
            }
        }

        // ---------- UserAgentHandler ----------

        [Test]
        public async Task SendDoesNotDuplicateUserAgentWhenAlreadyPresent()
        {
            using var inner = new CapturingHandler();
            var handler = new UserAgentHandler("MyApp/1.0") { InnerHandler = inner };
            using var client = new HttpClient(handler);

            using var req = new HttpRequestMessage(HttpMethod.Get, "http://x/y");
            req.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse("MyApp/1.0"));
            await client.SendAsync(req);

            inner.Last!.Headers.UserAgent.Should().HaveCount(1);
            inner.Last.Headers.UserAgent.ToString().Should().Be("MyApp/1.0");
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage Last { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Last = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
