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

        /// <summary>
        ///     Reads the <c>host</c> section. A missing section, missing keys and explicit nulls all leave the
        ///     defaults in place.
        /// </summary>
        public static HostConfiguration Load(IConfiguration configuration)
        {
            var hostConfiguration = configuration.BindToSection<HostConfiguration>(SectionName);
            hostConfiguration.HealthChecks ??= new HostHealthChecksConfiguration();
            hostConfiguration.HealthChecks.Memory ??= new HostMemoryHealthCheckConfiguration();
            return hostConfiguration;
        }
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
