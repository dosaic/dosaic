using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Dosaic.Hosting.WebHost.Tests
{
    /// <summary>
    ///     Builds the real host and drives the <c>host</c> section through environment variables.
    ///     Environment variables are process wide, so these tests never run next to anything else.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    public class HostConfigurationHostTests
    {
        private readonly List<string> _variables = [];

        [TearDown]
        public void Down()
        {
            foreach (var variable in _variables) Environment.SetEnvironmentVariable(variable, null);
            _variables.Clear();
        }

        private void Set(string variable, string value)
        {
            Environment.SetEnvironmentVariable(variable, value);
            _variables.Add(variable);
        }

        // the generator lists HostConfiguration for every consumer, because it carries a Dosaic attribute
        private static IHost BuildHost() => PluginWebHostBuilder.Create([typeof(HostConfiguration)]).Build();

        private static List<string> GetRegisteredHealthChecks(IHost host) => host.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(x => x.Name).ToList();

        [Test]
        public void HostConfigurationIsRegisteredOnceAsTheInstanceTheHostWasBuiltWith()
        {
            using var host = BuildHost();

            var hostConfiguration = host.Services.GetRequiredService<HostConfiguration>();
            host.Services.GetServices<HostConfiguration>().Should().ContainSingle()
                .Which.Should().BeSameAs(hostConfiguration);
            hostConfiguration.AdditionalConfigPaths.Should().NotBeNullOrWhiteSpace("the test appsettings set it");
            hostConfiguration.MaxRequestSize.Should().Be(HostConfiguration.DefaultMaxRequestSize);
            hostConfiguration.HealthChecks.Memory.MaximumMegabytes.Should().Be(2048);
        }

        [Test]
        public void DefaultsListenOnPort8080WithAnEightMegabyteLimitAndAllLivenessChecks()
        {
            using var host = BuildHost();

            host.Services.GetRequiredService<IConfiguration>()[WebHostDefaults.ServerUrlsKey].Should().Be("http://+:8080");
            host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize
                .Should().Be(8388608);
            GetRegisteredHealthChecks(host).Should().Contain(["api", "disk_space", "memory"]);
        }

        [Test]
        public void EnvironmentVariablesSetTheWholeHostSection()
        {
            Set("HOST_URLS", "http://127.0.0.1:5123,http://127.0.0.1:5124");
            Set("HOST_MAXREQUESTSIZE", "1024");
            Set("HOST_HEALTHCHECKS_MEMORY_MAXIMUMMEGABYTES", "0");
            using var host = BuildHost();

            host.Services.GetRequiredService<IConfiguration>()[WebHostDefaults.ServerUrlsKey].Should()
                .Be("http://127.0.0.1:5123;http://127.0.0.1:5124");
            host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize
                .Should().Be(1024);
            GetRegisteredHealthChecks(host).Should().Contain(["api", "disk_space"]).And.NotContain("memory");
            var hostConfiguration = host.Services.GetRequiredService<HostConfiguration>();
            hostConfiguration.Urls.Should().Be("http://127.0.0.1:5123,http://127.0.0.1:5124");
            hostConfiguration.HealthChecks.Memory.MaximumMegabytes.Should().Be(0);
        }

        [Test]
        public void AspNetCoreUrlsAreUsedWhenTheHostSectionHasNone()
        {
            Set("ASPNETCORE_URLS", "http://127.0.0.1:5125");
            using var host = BuildHost();

            host.Services.GetRequiredService<IConfiguration>()[WebHostDefaults.ServerUrlsKey].Should()
                .Be("http://127.0.0.1:5125");
        }

        [Test]
        public void AScalarHostVariableDoesNotBreakTheHost()
        {
            Set("HOST", "some-machine");
            using var host = BuildHost();

            host.Services.GetRequiredService<HostConfiguration>().MaxRequestSize.Should().Be(8388608);
        }
    }
}
