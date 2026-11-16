using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;

namespace Dosaic.Hosting.Abstractions.Configuration
{
    /// <summary>
    ///     The <c>host</c> section: everything the web host itself reads. It is loaded once when the host is
    ///     built, can be injected into plugin constructors and is registered as a singleton.
    /// </summary>
    [Configuration(SectionName)]
    public class HostConfiguration
    {
        public const string SectionName = "host";
        public const string DefaultUrls = "http://+:8080";
        public const long DefaultMaxRequestSize = 8388608;

        private long? _maxRequestSize;

        /// <summary>
        ///     The urls Kestrel listens on, separated by <c>,</c>. Null (default) falls back to
        ///     <c>aspnetcore:urls</c> and then to <see cref="DefaultUrls" />.
        /// </summary>
        public string Urls { get; set; }

        /// <summary>
        ///     Maximum request body size in bytes. Defaults to 8 MB; setting it to null restores the default.
        /// </summary>
        public long? MaxRequestSize
        {
            get => _maxRequestSize ?? DefaultMaxRequestSize;
            set => _maxRequestSize = value;
        }

        /// <summary>
        ///     Additional folders that are searched recursively for <c>appsettings.*</c> files, separated by
        ///     <c>,</c> or <c>;</c>. It is read before those files are loaded, so only the root appsettings
        ///     files, the command line and environment variables can set it.
        /// </summary>
        public string AdditionalConfigPaths { get; set; }

        /// <summary>The built-in liveness health checks.</summary>
        public HostHealthChecksConfiguration HealthChecks { get; set; } = new();

        /// <summary>Which proxies the forwarded headers (<c>X-Forwarded-*</c>) are accepted from.</summary>
        public HostForwardedHeadersConfiguration ForwardedHeaders { get; set; } = new();

        /// <summary>
        ///     Reads the <c>host</c> section. A missing section, missing keys and explicit nulls all leave the
        ///     defaults in place.
        /// </summary>
        public static HostConfiguration Load(IConfiguration configuration)
        {
            var hostConfiguration = configuration.BindToSection<HostConfiguration>(SectionName);
            hostConfiguration.HealthChecks ??= new HostHealthChecksConfiguration();
            hostConfiguration.HealthChecks.Memory ??= new HostMemoryHealthCheckConfiguration();
            hostConfiguration.ForwardedHeaders ??= new HostForwardedHeadersConfiguration();
            return hostConfiguration;
        }
    }

    public class HostForwardedHeadersConfiguration
    {
        /// <summary>
        ///     Loopback and the private address ranges (RFC 1918, carrier-grade NAT, IPv6 unique local). Pod and
        ///     node networks of Kubernetes clusters lie in these ranges, so an ingress controller in the cluster is
        ///     trusted without configuration, while a client on a public address cannot spoof the headers.
        /// </summary>
        public const string DefaultKnownNetworks =
            "127.0.0.0/8,::1/128,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,100.64.0.0/10,fc00::/7";

        private string _knownNetworks;

        /// <summary>
        ///     Networks (CIDR) whose requests may set the forwarded headers, separated by <c>,</c> or <c>;</c>.
        ///     Defaults to <see cref="DefaultKnownNetworks" />; setting it to null restores the default.
        ///     <c>0.0.0.0/0,::/0</c> trusts every sender.
        /// </summary>
        public string KnownNetworks
        {
            get => _knownNetworks ?? DefaultKnownNetworks;
            set => _knownNetworks = value;
        }

        /// <summary>Single proxy addresses that are trusted in addition, separated by <c>,</c> or <c>;</c>.</summary>
        public string KnownProxies { get; set; }

        /// <summary>
        ///     Maximum number of proxy hops taken from the headers. Null (default) walks back through the
        ///     <c>X-Forwarded-For</c> chain as long as the sender is trusted, which handles a load balancer in front
        ///     of the ingress controller and stops at the first untrusted address.
        /// </summary>
        public int? ForwardLimit { get; set; }
    }

    public class HostHealthChecksConfiguration
    {
        /// <summary>The <c>memory</c> liveness check.</summary>
        public HostMemoryHealthCheckConfiguration Memory { get; set; } = new();
    }

    public class HostMemoryHealthCheckConfiguration
    {
        public const int DefaultMaximumMegabytes = 1024 * 2;

        private int? _maximumMegabytes;

        /// <summary>
        ///     Allocated memory (<c>GC.GetTotalMemory</c>) above which the liveness check fails. Defaults to
        ///     2048; setting it to null restores the default. 0 does not register the check at all.
        /// </summary>
        public int? MaximumMegabytes
        {
            get => _maximumMegabytes ?? DefaultMaximumMegabytes;
            set => _maximumMegabytes = value;
        }
    }
}
