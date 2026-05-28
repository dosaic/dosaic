using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class OAuth2TokenProviderTests
    {
        private WireMockServer _server;
        private AuthenticationConfig _config;

        [SetUp]
        public void Setup()
        {
            _server = WireMockServer.Start();
            _config = new AuthenticationConfig
            {
                Enabled = true,
                BaseUrl = _server.Url,
                TokenUrlPath = "/auth/token",
                ClientId = "id",
                ClientSecret = "secret",
                GrantType = GrantType.ClientCredentials,
                RefreshSkew = TimeSpan.Zero
            };
        }

        [TearDown]
        public void TearDown() => _server?.Dispose();

        [Test]
        public async Task ConcurrentCallsTriggerSingleTokenFetch()
        {
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .RespondWith(Response.Create()
                    .WithDelay(TimeSpan.FromMilliseconds(80))
                    .WithSuccess()
                    .WithBodyAsJson(new { token_type = "Bearer", access_token = "tok", expires_in = 300 }));

            var provider = OAuth2TokenProvider.Create(_config, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var tasks = Enumerable.Range(0, 20).Select(_ => provider.GetTokenAsync(false, CancellationToken.None)).ToArray();
            var results = await Task.WhenAll(tasks);

            results.Should().AllSatisfy(t => t.Value.Should().Be("tok"));
            results.Length.Should().Be(20);
            _server.FindLogEntries(Request.Create().WithPath("/auth/token").UsingPost()).Should().HaveCount(1);
        }

        [Test]
        public async Task InvalidateForcesNewFetch()
        {
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .InScenario("inv")
                .WillSetStateTo("second")
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "tok1", expires_in = 300 }));
            _server.Given(Request.Create().WithPath("/auth/token").UsingPost())
                .InScenario("inv")
                .WhenStateIs("second")
                .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { token_type = "Bearer", access_token = "tok2", expires_in = 300 }));

            var provider = OAuth2TokenProvider.Create(_config, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("tok1");
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("tok1");
            provider.Invalidate();
            (await provider.GetTokenAsync(false, CancellationToken.None)).Value.Should().Be("tok2");
        }
    }
}
