using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Configuration
{
    public class HostConfigurationTests
    {
        private static HostConfiguration Load(Dictionary<string, string> values) =>
            HostConfiguration.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        [Test]
        public void IsBoundToTheHostSection()
        {
            typeof(HostConfiguration).GetAttribute<ConfigurationAttribute>()!.Section.Should().Be("host");
        }

        [Test]
        public void MissingSectionGivesTheDefaults()
        {
            var config = Load([]);

            config.Urls.Should().BeNull();
            config.MaxRequestSize.Should().Be(8388608);
            config.AdditionalConfigPaths.Should().BeNull();
            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(2048);
        }

        [Test]
        public void EveryKeyIsBound()
        {
            var config = Load(new Dictionary<string, string>
            {
                ["host:urls"] = "http://+:5000,http://+:5001",
                ["host:maxRequestSize"] = "1024",
                ["host:additionalConfigPaths"] = "/config;/secrets",
                ["host:healthChecks:memory:maximumMegabytes"] = "6144"
            });

            config.Urls.Should().Be("http://+:5000,http://+:5001");
            config.MaxRequestSize.Should().Be(1024);
            config.AdditionalConfigPaths.Should().Be("/config;/secrets");
            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(6144);
        }

        [Test]
        public void EnvironmentStyleKeysAreBound()
        {
            var config = Load(new Dictionary<string, string>
            {
                ["HOST:URLS"] = "http://+:5000",
                ["HOST:MAXREQUESTSIZE"] = "2048",
                ["HOST:ADDITIONALCONFIGPATHS"] = "/config",
                ["HOST:HEALTHCHECKS:MEMORY:MAXIMUMMEGABYTES"] = "0"
            });

            config.Urls.Should().Be("http://+:5000");
            config.MaxRequestSize.Should().Be(2048);
            config.AdditionalConfigPaths.Should().Be("/config");
            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(0);
        }

        [Test]
        public void ExplicitNullsKeepTheDefaults()
        {
            var config = Load(new Dictionary<string, string>
            {
                ["host:urls"] = null,
                ["host:maxRequestSize"] = null,
                ["host:healthChecks:memory:maximumMegabytes"] = null
            });

            config.Urls.Should().BeNull();
            config.MaxRequestSize.Should().Be(HostConfiguration.DefaultMaxRequestSize);
            config.HealthChecks.Memory.MaximumMegabytes.Should()
                .Be(HostMemoryHealthCheckConfiguration.DefaultMaximumMegabytes);
        }

        [TestCase("host:healthChecks")]
        [TestCase("host:healthChecks:memory")]
        public void EmptyNestedSectionsKeepTheDefaults(string key)
        {
            var config = Load(new Dictionary<string, string> { [key] = "" });

            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(2048);
        }

        [Test]
        public void AScalarHostValueFromTheEnvironmentDoesNotBreakTheSection()
        {
            var config = Load(new Dictionary<string, string>
            {
                ["HOST"] = "some-machine",
                ["HOST:MAXREQUESTSIZE"] = "4096"
            });

            config.MaxRequestSize.Should().Be(4096);
            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(2048);
            Load(new Dictionary<string, string> { ["HOST"] = "some-machine" }).MaxRequestSize.Should().Be(8388608);
        }

        [Test]
        public void SettingAValueToNullRestoresItsDefault()
        {
            var config = new HostConfiguration { MaxRequestSize = 1 };
            config.HealthChecks.Memory.MaximumMegabytes = 1;

            config.MaxRequestSize = null;
            config.HealthChecks.Memory.MaximumMegabytes = null;

            config.MaxRequestSize.Should().Be(8388608);
            config.HealthChecks.Memory.MaximumMegabytes.Should().Be(2048);
        }
    }
}
