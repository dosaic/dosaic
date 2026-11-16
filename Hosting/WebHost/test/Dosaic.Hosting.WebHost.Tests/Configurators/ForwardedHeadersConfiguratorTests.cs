using System.Net;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.WebHost.Configurators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Dosaic.Hosting.WebHost.Tests.Configurators
{
    public class ForwardedHeadersConfiguratorTests
    {
        private static ForwardedHeadersOptions CreateOptions(HostForwardedHeadersConfiguration configuration)
        {
            var options = new ForwardedHeadersOptions();
            new ForwardedHeadersConfigurator(configuration).Configure(options);
            return options;
        }

        private static async Task<HttpContext> SendAsync(ForwardedHeadersOptions options, string remoteIp,
            string forwardedFor = "203.0.113.7", string proto = "https", string host = "app.example.com",
            string prefix = "/p/my-app/api")
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("my-app.namespace.svc:8080");
            context.Request.Path = "/hangfire";
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            context.Request.Headers["X-Forwarded-Proto"] = proto;
            context.Request.Headers["X-Forwarded-Host"] = host;
            context.Request.Headers["X-Forwarded-Prefix"] = prefix;
            var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
                Options.Create(options));
            await middleware.Invoke(context);
            return context;
        }

        [Test]
        public void DefaultsTrustLoopbackAndThePrivateNetworksWithoutLimit()
        {
            var options = CreateOptions(new HostForwardedHeadersConfiguration());

            options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
            options.ForwardLimit.Should().BeNull();
            options.KnownProxies.Should().BeEmpty();
            options.KnownIPNetworks.Select(x => x.ToString()).Should().BeEquivalentTo(
                "127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "100.64.0.0/10",
                "fc00::/7");
        }

        [Test]
        public void NullConfigurationUsesTheDefaults()
        {
            CreateOptions(null).KnownIPNetworks.Should().HaveCount(7);
        }

        [TestCase("10.42.0.17")]
        [TestCase("172.20.3.4")]
        [TestCase("192.168.1.10")]
        [TestCase("100.64.12.1")]
        [TestCase("127.0.0.1")]
        [TestCase("::ffff:10.42.0.17")]
        [TestCase("fd00:10:244::5")]
        public async Task HeadersOfAProxyInAPrivateNetworkAreApplied(string remoteIp)
        {
            var context = await SendAsync(CreateOptions(new HostForwardedHeadersConfiguration()), remoteIp);

            context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
            context.Request.Scheme.Should().Be("https");
            context.Request.Host.Value.Should().Be("app.example.com");
            context.Request.PathBase.Value.Should().Be("/p/my-app/api");
            context.Request.Path.Value.Should().Be("/hangfire");
        }

        [Test]
        public async Task HeadersOfAPublicSenderAreIgnored()
        {
            var context = await SendAsync(CreateOptions(new HostForwardedHeadersConfiguration()), "198.51.100.20");

            context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.20"));
            context.Request.Scheme.Should().Be("http");
            context.Request.Host.Value.Should().Be("my-app.namespace.svc:8080");
            context.Request.PathBase.Value.Should().BeEmpty();
        }

        [Test]
        public async Task ChainIsWalkedBackThroughTrustedProxiesOnly()
        {
            var options = CreateOptions(new HostForwardedHeadersConfiguration());

            var behindLoadBalancer = await SendAsync(options, "10.42.0.17", "203.0.113.7, 10.0.0.5",
                proto: "https, https", host: "app.example.com, app.example.com", prefix: "/p/my-app/api, /p/my-app/api");
            var spoofed = await SendAsync(options, "10.42.0.17", "192.0.2.1, 203.0.113.7");

            behindLoadBalancer.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
            spoofed.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
        }

        [Test]
        public async Task ConfiguredNetworksAndProxiesReplaceTheDefaults()
        {
            var options = CreateOptions(new HostForwardedHeadersConfiguration
            {
                KnownNetworks = " 10.42.0.0/16 ; ",
                KnownProxies = "198.51.100.20, 2001:db8::1",
                ForwardLimit = 1
            });

            options.KnownIPNetworks.Select(x => x.ToString()).Should().BeEquivalentTo("10.42.0.0/16");
            options.KnownProxies.Should().BeEquivalentTo([IPAddress.Parse("198.51.100.20"), IPAddress.Parse("2001:db8::1")]);
            options.ForwardLimit.Should().Be(1);
            (await SendAsync(options, "198.51.100.20")).Request.Scheme.Should().Be("https");
            (await SendAsync(options, "192.168.1.10")).Request.Scheme.Should().Be("http");
        }

        [Test]
        public void SettingTheNetworksToNullRestoresTheDefault()
        {
            new HostForwardedHeadersConfiguration { KnownNetworks = null }.KnownNetworks.Should()
                .Be(HostForwardedHeadersConfiguration.DefaultKnownNetworks);
        }

        [Test]
        public void InvalidNetworkStopsTheStart()
        {
            var action = () => new ForwardedHeadersConfigurator(new HostForwardedHeadersConfiguration
            {
                KnownNetworks = "10.0.0.0/8,10.0.0.0"
            });

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*host:forwardedHeaders:knownNetworks*'10.0.0.0'*");
        }

        [Test]
        public void InvalidProxyStopsTheStart()
        {
            var action = () => new ForwardedHeadersConfigurator(new HostForwardedHeadersConfiguration
            {
                KnownProxies = "ingress"
            });

            action.Should().Throw<InvalidOperationException>().WithMessage("*host:forwardedHeaders:knownProxies*");
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ForwardLimitBelowOneStopsTheStart(int limit)
        {
            var action = () => new ForwardedHeadersConfigurator(new HostForwardedHeadersConfiguration
            {
                ForwardLimit = limit
            });

            action.Should().Throw<InvalidOperationException>().WithMessage("*host:forwardedHeaders:forwardLimit*");
        }
    }
}
