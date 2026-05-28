using System.Net;
using System.Net.Http;
using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using NUnit.Framework;
using Polly;
using Polly.Retry;
using RestEase;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class RestClientFactoryTests
    {
        private const string AuthorizationHeader = "Authorization";
        private const string TokenPath = "/auth/token";

        private string _baseAddress;
        private WireMockServer _server;

        [SetUp]
        public void Load()
        {
            _server = WireMockServer.Start();
            _baseAddress = _server.Url;
        }

        [TearDown]
        public void Unload()
        {
            _server?.Dispose();
        }

        [Test]
        public async Task CanCreateRestClientWithBaseAddress()
        {
            var name = "the name";
            var returnObj = new SomeResource { Id = Guid.NewGuid(), Name = name };
            var serverRequest = Request.Create().WithPath("/").UsingPost();
            _server.Given(serverRequest)
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(returnObj)));

            var client = RestClientFactory.Create<ISomeApi>(_baseAddress);
            client.Should().NotBeNull();
            var result = await client.Create(new SomeResource { Name = name }, CancellationToken.None);
            result.Id.Should().Be(returnObj.Id);
            result.Name.Should().Be(returnObj.Name);

#pragma warning disable CA1826
            var logEntry = _server.FindLogEntries(serverRequest).First();
#pragma warning restore CA1826
            logEntry.RequestMessage.Headers.Should().NotContainKey(AuthorizationHeader);
        }

        [Test]
        public async Task CanCreateRestClientWithBaseAddressAndAuthenticationConfig()
        {
            var name = "the name";
            var token = "abc";

            _server.Given(Request.Create().WithPath(TokenPath).UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = token, expires_in = 60 }));
            var returnObj = new SomeResource { Id = Guid.NewGuid(), Name = name };
            var serverRequest = Request.Create().WithPath("/").UsingPost();
            _server.Given(serverRequest)
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(returnObj)));

            var authConfig = new AuthenticationConfig
            {
                Enabled = true,
                TokenUrlPath = TokenPath,
                BaseUrl = _baseAddress,
                ClientId = "clientId",
                ClientSecret = "ClientSecret",
                GrantType = GrantType.ClientCredentials
            };
            var client = RestClientFactory.Create<ISomeApi>(_baseAddress, authConfig);
            client.Should().NotBeNull();
            var result = await client.Create(new SomeResource { Name = name }, CancellationToken.None);
            result.Id.Should().Be(returnObj.Id);
            result.Name.Should().Be(returnObj.Name);

#pragma warning disable CA1826
            var logEntry = _server.FindLogEntries(serverRequest).First();
#pragma warning restore CA1826
            logEntry.RequestMessage.Headers.Should().ContainKey(AuthorizationHeader);
            logEntry.RequestMessage.Headers[AuthorizationHeader].Single().Should().Be("Bearer " + token);
        }

        [Test]
        public async Task DefaultPipelineRetriesOn500()
        {
            var id = Guid.NewGuid();
            var requestMatcher = Request.Create().WithPath($"/{id}").UsingDelete();
            _server.Given(requestMatcher)
                .InScenario("retry")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(500));
            _server.Given(requestMatcher)
                .InScenario("retry")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess());

            var client = RestClientFactory.Create<ISomeApi>(_baseAddress);
            await client.Delete(id, CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public void ExceptionsGetThrownOnFailedRequests()
        {
            var id = Guid.NewGuid();
            var requestMatcher = Request.Create().WithPath($"/{id}").UsingPut();
            _server.Given(requestMatcher).RespondWith(Response.Create().WithStatusCode(500));

            var client = RestClientFactory.Create<ISomeApi>(_baseAddress);
            var apiException = Assert.ThrowsAsync<ApiException>(async () => await client.Update(id, new SomeResource(), CancellationToken.None))!;
            apiException.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(4);
        }

        [Test]
        public async Task CustomPipelineCanBeApplied()
        {
            var id = Guid.NewGuid();
            var requestMatcher = Request.Create().WithPath($"/{id}").UsingDelete();
            _server.Given(requestMatcher)
                .InScenario("custom")
                .WillSetStateTo(1)
                .RespondWith(Response.Create().WithStatusCode(409));
            _server.Given(requestMatcher)
                .InScenario("custom")
                .WhenStateIs(1)
                .RespondWith(Response.Create().WithSuccess());

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.Zero,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(r => r.StatusCode == HttpStatusCode.Conflict)
                })
                .Build();

            var client = RestClientFactory.Create<ISomeApi>(_baseAddress, pipeline);
            await client.Delete(id, CancellationToken.None);
            _server.FindLogEntries(requestMatcher).Should().HaveCount(2);
        }

        [Test]
        public async Task AuthHandlerRefreshesTheAccessToken()
        {
            var authConfig = new AuthenticationConfig
            {
                Enabled = true,
                TokenUrlPath = TokenPath,
                BaseUrl = _baseAddress,
                ClientId = "test",
                ClientSecret = "secret",
                GrantType = GrantType.Password,
                Username = "u",
                Password = "p",
                RefreshSkew = TimeSpan.Zero
            };

            _server.Given(Request.Create()
                    .WithPath(TokenPath)
                    .WithBody((string content) => content.Contains("grant_type=password"))
                    .UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "123", expires_in = 1, refresh_expires_in = 70, refresh_token = "refreshX" }));
            _server.Given(Request.Create()
                    .WithPath(TokenPath)
                    .WithBody((string content) => content.Contains("grant_type=refresh_token"))
                    .UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "new123", expires_in = 60, refresh_expires_in = 70, refresh_token = "refreshX" }));

            var first = new SomeResource { Id = Guid.NewGuid(), Name = "first" };
            var second = new SomeResource { Id = Guid.NewGuid(), Name = "second" };
            _server.Given(Request.Create().WithPath("/").WithHeader(AuthorizationHeader, "Bearer 123").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(first)));
            _server.Given(Request.Create().WithPath("/").WithHeader(AuthorizationHeader, "Bearer new123").UsingPost())
                .RespondWith(Response.Create().WithSuccess().WithBody(JsonSerializer.Serialize(second)));

            var client = RestClientFactory.Create<ISomeApi>(_baseAddress, authConfig);
            var firstResult = await client.Create(new SomeResource(), CancellationToken.None);
            firstResult.Name.Should().Be("first");
            await Task.Delay(1100);
            var secondResult = await client.Create(new SomeResource(), CancellationToken.None);
            secondResult.Name.Should().Be("second");
        }
    }
}
