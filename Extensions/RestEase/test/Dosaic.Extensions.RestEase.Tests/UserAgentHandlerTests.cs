using System.Net;
using System.Net.Http;
using AwesomeAssertions;
using Dosaic.Extensions.RestEase.Handlers;
using NUnit.Framework;

namespace Dosaic.Extensions.RestEase.Tests
{
    public sealed class UserAgentHandlerTests
    {
        [Test]
        public async Task AppliesUserAgentWhenConfigured()
        {
            using var inner = new RecordingHandler();
            var handler = new UserAgentHandler("MyApp/1.0") { InnerHandler = inner };
            using var client = new HttpClient(handler);
            await client.GetAsync("http://x/y");

            inner.Last.Headers.UserAgent.ToString().Should().Be("MyApp/1.0");
        }

        [Test]
        public async Task DoesNotDuplicateUserAgentOnRetry()
        {
            using var inner = new RecordingHandler();
            var handler = new UserAgentHandler("MyApp/1.0") { InnerHandler = inner };
            using var client = new HttpClient(handler);
            using var req = new HttpRequestMessage(HttpMethod.Get, "http://x/y");
            await client.SendAsync(req);
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://x/y")
            {
                Headers = { { "X-Probe", "1" } }
            });
            inner.Last.Headers.UserAgent.Should().HaveCount(1);
        }

        [Test]
        public async Task NoUserAgentWhenConfiguredEmpty()
        {
            using var inner = new RecordingHandler();
            var handler = new UserAgentHandler(null) { InnerHandler = inner };
            using var client = new HttpClient(handler);
            await client.GetAsync("http://x/y");
            inner.Last.Headers.UserAgent.Should().BeEmpty();
        }

        private sealed class RecordingHandler : HttpMessageHandler
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
