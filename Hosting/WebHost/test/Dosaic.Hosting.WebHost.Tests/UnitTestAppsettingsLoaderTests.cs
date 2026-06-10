using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Assertions;
using NUnit.Framework;
using static Dosaic.Hosting.WebHost.Tests.TypeImplementationResolverTests;

namespace Dosaic.Hosting.WebHost.Tests
{
    public class UnitTestAppsettingsLoaderTests
    {
        [Test]
        public void LoadLocalAppsettingsShouldLoadAppsettings()
        {
            var result = UnitTestAppsettingsLoader.LoadSection<UnitTestPluginConfig>();

            result.Should().NotBeNull();
            result.Name.Should().Be("example1");
            result.Number.Should().Be(42);
            result.NumberAsString.Should().Be("42");
        }

        [Test]
        public void LoadSectionShouldLoadSectionFromAppsettings()
        {
            var additionalFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testsettings.json");
            File.WriteAllText(additionalFile, @"{
                ""TestSettings"": {
                    ""Value"": ""test-value""
                }
            }");

            var result = UnitTestAppsettingsLoader.LoadSection<TestSettings>(additionalFiles: new[] { additionalFile });

            result.Value.Should().Be("test-value");
        }

    }

    [Configuration("TestSettings")]
    public class TestSettings
    {
        public string Value { get; set; }
    }
}
