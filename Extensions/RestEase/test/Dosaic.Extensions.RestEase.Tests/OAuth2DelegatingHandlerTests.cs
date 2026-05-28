using System.Net;
using System.Net.Http;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Authentication;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class OAuth2DelegatingHandlerTests
    {
        [Test]
        public async Task AppliesBearerTokenAndForwards()
        {
            var provider = Substitute.For<ITokenProvider>();
            provider.GetTokenAsync(false, Arg.Any<CancellationToken>())
                .Returns(new AccessToken { TokenType = "Bearer", Value = "tok-1", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });

            using var inner = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var handler = new OAuth2DelegatingHandler(provider) { InnerHandler = inner };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync("http://x/y");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            inner.Requests.Should().HaveCount(1);
            inner.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
            inner.Requests[0].Headers.Authorization!.Parameter.Should().Be("tok-1");
            provider.DidNotReceive().Invalidate();
            await provider.Received(0).GetTokenAsync(true, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task On401InvalidatesAndRetriesOnceWithForcedRefresh()
        {
            var provider = Substitute.For<ITokenProvider>();
            provider.GetTokenAsync(false, Arg.Any<CancellationToken>())
                .Returns(new AccessToken { TokenType = "Bearer", Value = "stale", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            provider.GetTokenAsync(true, Arg.Any<CancellationToken>())
                .Returns(new AccessToken { TokenType = "Bearer", Value = "fresh", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });

            var attempt = 0;
            var observedTokens = new List<string>();
            using var inner = new RecordingHandler(req =>
            {
                attempt++;
                observedTokens.Add(req.Headers.Authorization!.Parameter);
                return new HttpResponseMessage(attempt == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
            });
            var handler = new OAuth2DelegatingHandler(provider) { InnerHandler = inner };
            using var client = new HttpClient(handler);
            var response = await client.GetAsync("http://x/y");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            observedTokens.Should().Equal("stale", "fresh");
            provider.Received(1).Invalidate();
        }

        [Test]
        public async Task DoesNotOverrideExistingAuthorizationHeader()
        {
            var provider = Substitute.For<ITokenProvider>();
            using var inner = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var handler = new OAuth2DelegatingHandler(provider) { InnerHandler = inner };
            using var client = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "http://x/y");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "user-supplied");
            var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            inner.Requests.Should().HaveCount(1);
            inner.Requests[0].Headers.Authorization!.Parameter.Should().Be("user-supplied");
            await provider.DidNotReceive().GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<HttpRequestMessage> Requests { get; } = new();

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_responder(request));
            }
        }
    }
}
