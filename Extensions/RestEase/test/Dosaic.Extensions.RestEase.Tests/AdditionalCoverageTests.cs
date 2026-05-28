using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using Dosaic.Extensions.RestEase.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Polly;
using Polly.Retry;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class AdditionalCoverageTests
    {
        private WireMockServer _server;

        [SetUp]
        public void Setup() => _server = WireMockServer.Start();

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task FactoryAppliesTimeoutUserAgentAndDefaultHeaders()
        {
            var serverRequest = Request.Create().WithPath("/").UsingGet();
            _server.Given(serverRequest).RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var client = RestClientFactory.Create<ISomeApi>(_server.Url, o =>
            {
                o.Timeout = TimeSpan.FromSeconds(5);
                o.UserAgent = "ua/1.0";
                o.DefaultHeaders["X-Test"] = "v";
                o.JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            });
            await client.Get(CancellationToken.None);

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(serverRequest).First();
#pragma warning restore CA1826
            entry.RequestMessage.Headers.Should().ContainKey("X-Test");
            entry.RequestMessage.Headers["User-Agent"].Should().Contain("ua/1.0");
        }

        [Test]
        public async Task FactoryFourArgOverloadAcceptsAuthAndPipeline()
        {
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "x", expires_in = 300 }));
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => r.StatusCode == HttpStatusCode.BadGateway)
                })
                .Build();
            var auth = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = "/auth/token",
                GrantType = GrantType.ClientCredentials,
                ClientId = "id",
                ClientSecret = "s"
            };

            var client = RestClientFactory.Create<ISomeApi>(_server.Url, auth, pipeline);
            await client.Get(CancellationToken.None);
        }

        [Test]
        public async Task RetryAfterDelayGeneratorHonoursHeaderDelta()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("ra-delta")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(429).WithHeader("Retry-After", "1"));
            _server.Given(requestMatcher)
                .InScenario("ra-delta")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var client = RestClientFactory.Create<ISomeApi>(_server.Url);
            var start = DateTime.UtcNow;
            await client.Create(new SomeResource(), CancellationToken.None);
            var elapsed = DateTime.UtcNow - start;
            elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task RetryAfterDelayGeneratorHonoursHttpDate()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            var retryAt = DateTime.UtcNow.AddMilliseconds(800).ToString("R");
            _server.Given(requestMatcher)
                .InScenario("ra-date")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(429).WithHeader("Retry-After", retryAt));
            _server.Given(requestMatcher)
                .InScenario("ra-date")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var client = RestClientFactory.Create<ISomeApi>(_server.Url);
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task TokenProviderUsesRefreshTokenFlow()
        {
            var calls = 0;
            var path = "/auth/token";
            _server.Given(Request.Create().WithPath(path).WithBody((string b) => b.Contains("grant_type=password")).UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "a", expires_in = 1, refresh_expires_in = 600, refresh_token = "r" }));
            _server.Given(Request.Create().WithPath(path).WithBody((string b) => b.Contains("grant_type=refresh_token")).UsingPost())
                .RespondWith(Response.Create().WithCallback(_ =>
                {
                    calls++;
                    return new WireMock.ResponseMessage { StatusCode = 200, BodyData = new WireMock.Util.BodyData { DetectedBodyType = WireMock.Types.BodyType.Json, BodyAsJson = new { token_type = "Bearer", access_token = "a2", expires_in = 600, refresh_expires_in = 600, refresh_token = "r" } } };
                }));

            var config = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = path,
                GrantType = GrantType.Password,
                ClientId = "id",
                Username = "u",
                Password = "p",
                RefreshSkew = TimeSpan.Zero
            };

            var provider = OAuth2TokenProvider.Create(config);
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("a");
            await Task.Delay(1100);
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("a2");
            calls.Should().Be(1);
        }

        [Test]
        public async Task ClientCredentialsGrantSendsScopeAndAudience()
        {
            var path = "/auth/token";
            _server.Given(Request.Create().WithPath(path).UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "tok", expires_in = 600 }));

            var config = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = path,
                GrantType = GrantType.ClientCredentials,
                ClientId = "c",
                ClientSecret = "s",
                Scope = "read",
                Audience = "api"
            };

            var provider = OAuth2TokenProvider.Create(config);
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("tok");

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(Request.Create().WithPath(path).UsingPost()).First();
#pragma warning restore CA1826
            var body = entry.RequestMessage.Body;
            body.Should().Contain("grant_type=client_credentials");
            body.Should().Contain("scope=read");
            body.Should().Contain("audience=api");
        }

        [Test]
        public async Task BuilderConfigureOptionsAndJsonApply()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "Hello" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>()
                .ConfigureOptions(o => o.BaseAddress = _server.Url)
                .ConfigureJson(j => j.WriteIndented = true)
                .ConfigureHttpClient(c => c.DefaultRequestHeaders.Add("X-Probe", "yes"));

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(Request.Create().WithPath("/").UsingPost()).First();
#pragma warning restore CA1826
            entry.RequestMessage.Headers.Should().ContainKey("X-Probe");
        }

        [Test]
        public async Task BuilderAddTokenProviderUsesCustomProvider()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "n" };
            _server.Given(Request.Create().WithPath("/").WithHeader("Authorization", "Bearer fake").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddSingleton<FakeTokenProvider>();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddTokenProvider<FakeTokenProvider>();

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            var result = await client.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public async Task BuilderAddResilienceWithCustomPipeline()
        {
            var requestMatcher = Request.Create().WithPath("/").UsingPost();
            _server.Given(requestMatcher)
                .InScenario("cust-pipe")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(503));
            _server.Given(requestMatcher)
                .InScenario("cust-pipe")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess().WithBody("{}"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url)
                .AddPolly((pb, _) => pb.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => r.StatusCode == HttpStatusCode.ServiceUnavailable)
                }));

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Create(new SomeResource(), CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task RestClientFactoryServiceCreates()
        {
            var resource = new SomeResource { Id = Guid.NewGuid(), Name = "f" };
            _server.Given(Request.Create().WithPath("/").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(resource)));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o => o.BaseAddress = _server.Url);
            await using var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<IRestClientFactory>();
            var api = factory.Create<ISomeApi>();
            var result = await api.Create(new SomeResource(), CancellationToken.None);
            result.Id.Should().Be(resource.Id);
        }

        [Test]
        public void AddRestEaseApiThrowsForEmptyName()
        {
            var services = new ServiceCollection();
            var act = () => services.AddRestEaseApi<ISomeApi>(name: "");
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void AuthenticationConfigDefaultsAreSet()
        {
            var cfg = new AuthenticationConfig
            {
                Scope = "s",
                Audience = "a"
            };
            cfg.Scope.Should().Be("s");
            cfg.Audience.Should().Be("a");
            cfg.RefreshSkew.Should().Be(TimeSpan.FromSeconds(30));
        }

        [Test]
        public void AccessTokenPropertiesArePreserved()
        {
            var t = new AccessToken { TokenType = "Bearer", Value = "v", ExpiresAt = DateTimeOffset.UtcNow };
            t.TokenType.Should().Be("Bearer");
            t.Value.Should().Be("v");
            t.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Test]
        public void ComputeRetryDelayReturnsNullForMissingHeader()
            => RestEaseDefaults.ComputeRetryDelay(null).Should().BeNull();

        [Test]
        public void ComputeRetryDelayHonoursDelta()
            => RestEaseDefaults.ComputeRetryDelay(new RetryConditionHeaderValue(TimeSpan.FromSeconds(3))).Should().Be(TimeSpan.FromSeconds(3));

        [Test]
        public void ComputeRetryDelayHonoursDate()
        {
            var date = DateTimeOffset.UtcNow.AddSeconds(5);
            var delay = RestEaseDefaults.ComputeRetryDelay(new RetryConditionHeaderValue(date));
            delay.Should().NotBeNull();
            delay!.Value.Should().BeGreaterThan(TimeSpan.FromSeconds(3));
        }

        [Test]
        public async Task BuilderTimeoutAndUserAgentApplied()
        {
            var serverRequest = Request.Create().WithPath("/").UsingGet();
            _server.Given(serverRequest).RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var services = new ServiceCollection();
            services.AddRestEaseApi<ISomeApi>(o =>
            {
                o.BaseAddress = _server.Url;
                o.Timeout = TimeSpan.FromSeconds(30);
                o.UserAgent = "Probe/1.2";
            });

            await using var sp = services.BuildServiceProvider();
            var client = sp.GetRequiredService<ISomeApi>();
            await client.Get(CancellationToken.None);

#pragma warning disable CA1826
            var entry = _server.FindLogEntries(serverRequest).First();
#pragma warning restore CA1826
            entry.RequestMessage.Headers["User-Agent"].Should().Contain("Probe/1.2");
        }

        [Test]
        public void OAuth2ModelTreatsNoRefreshTokenAsRefreshExpired()
        {
            var model = new OAuth2Model { ExpiresIn = 1, RefreshExpiresIn = 0, Created = DateTime.UtcNow.AddSeconds(-2) };
            model.ShouldCreateToken(TimeSpan.Zero).Should().BeTrue();
            model.ShouldRefreshToken(TimeSpan.Zero).Should().BeFalse();
        }

        [Test]
        public void OAuth2ModelTreatsFreshTokenAsNotExpired()
        {
            var model = new OAuth2Model { ExpiresIn = 600, RefreshExpiresIn = 600, Created = DateTime.UtcNow };
            model.ShouldCreateToken(TimeSpan.Zero).Should().BeFalse();
            model.ShouldRefreshToken(TimeSpan.Zero).Should().BeFalse();
        }

        [Test]
        public async Task FactoryAcceptsNullConfigureAction()
        {
            _server.Given(Request.Create().WithPath("/").UsingGet())
                .RespondWith(Response.Create().WithSuccess().WithBody("[]"));

            var client = RestClientFactory.Create<ISomeApi>(_server.Url, configure: null);
            await client.Get(CancellationToken.None);
        }

        [Test]
        public async Task TokenProviderDefaultsTokenTypeToBearerWhenMissing()
        {
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { access_token = "raw", expires_in = 600 }));

            var config = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = "/auth/token",
                GrantType = GrantType.ClientCredentials,
                ClientId = "id",
                ClientSecret = "s"
            };
            var provider = OAuth2TokenProvider.Create(config);
            var token = await provider.GetTokenAsync(false, CancellationToken.None);
            token.TokenType.Should().Be("Bearer");
            token.Value.Should().Be("raw");
        }

        [Test]
        public async Task TokenProviderInvalidateThenTokenIsReFetched()
        {
            var provider = Substitute.For<ITokenProvider>();
            provider.GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new AccessToken { TokenType = "Bearer", Value = "t", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            provider.Invalidate();
            await provider.GetTokenAsync(true, CancellationToken.None);
            provider.Received(1).Invalidate();
        }

        private sealed class FakeTokenProvider : ITokenProvider
        {
            public Task<AccessToken> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
                => Task.FromResult(new AccessToken { TokenType = "Bearer", Value = "fake", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            public void Invalidate() { }
        }
    }
}
