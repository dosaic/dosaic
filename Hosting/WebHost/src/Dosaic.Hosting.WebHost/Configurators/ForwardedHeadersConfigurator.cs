using System.Net;
using Dosaic.Hosting.Abstractions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Dosaic.Hosting.WebHost.Configurators
{
    /// <summary>
    ///     Accepts every forwarded header (<c>X-Forwarded-For</c>, <c>-Proto</c>, <c>-Host</c>, <c>-Prefix</c>) from the
    ///     networks and proxies of <c>host:forwardedHeaders</c>. ASP.NET Core's own default trusts loopback only,
    ///     which ignores the headers of an ingress controller in a Kubernetes cluster.
    /// </summary>
    internal sealed class ForwardedHeadersConfigurator
    {
        internal const string KnownNetworksKey = "host:forwardedHeaders:knownNetworks";
        internal const string KnownProxiesKey = "host:forwardedHeaders:knownProxies";
        internal const string ForwardLimitKey = "host:forwardedHeaders:forwardLimit";

        private readonly IPNetwork[] _knownNetworks;
        private readonly IPAddress[] _knownProxies;
        private readonly int? _forwardLimit;

        /// <exception cref="InvalidOperationException">A network, an address or the limit is invalid.</exception>
        public ForwardedHeadersConfigurator(HostForwardedHeadersConfiguration configuration)
        {
            configuration ??= new HostForwardedHeadersConfiguration();
            _knownNetworks = Split(configuration.KnownNetworks).Select(ParseNetwork).ToArray();
            _knownProxies = Split(configuration.KnownProxies).Select(ParseProxy).ToArray();
            if (configuration.ForwardLimit is < 1)
                throw new InvalidOperationException(
                    $"Configuration: {ForwardLimitKey} must be 1 or more, or empty for no limit, but is {configuration.ForwardLimit}.");
            _forwardLimit = configuration.ForwardLimit;
        }

        public void Configure(ForwardedHeadersOptions options)
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.ForwardLimit = _forwardLimit;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in _knownNetworks) options.KnownIPNetworks.Add(network);
            foreach (var proxy in _knownProxies) options.KnownProxies.Add(proxy);
        }

        private static IEnumerable<string> Split(string value) =>
            (value ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static IPNetwork ParseNetwork(string value) =>
            IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException(
                    $"Configuration: {KnownNetworksKey} contains '{value}', which is no network in CIDR notation such as 10.0.0.0/8.");

        private static IPAddress ParseProxy(string value) =>
            IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException(
                    $"Configuration: {KnownProxiesKey} contains '{value}', which is no IP address.");
    }
}
