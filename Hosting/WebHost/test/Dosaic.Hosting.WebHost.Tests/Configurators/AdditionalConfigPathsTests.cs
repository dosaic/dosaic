using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.WebHost.Configurators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.Json;
using NetEscapades.Configuration.Yaml;
using NUnit.Framework;

namespace Dosaic.Hosting.WebHost.Tests.Configurators
{
    public class AdditionalConfigPathsTests
    {
        private const string AdditionalconfigTestvalue = "TESTCONFIG_TESTVALUE";
        private string _testConfigFolder = null!;

        [SetUp]
        public void Setup()
        {
            _testConfigFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestConfigFolder");
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, null);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, null);
            Environment.SetEnvironmentVariable(AdditionalconfigTestvalue, null);
        }

        private static IConfiguration BuildConfiguration()
        {
            var configManager = new ConfigurationManager();

            HostConfigurator.ConfigureAppConfiguration(configManager, []);

            return configManager;
        }

        [Test]
        public void ShouldLoadConfigFromSublFolderViaEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, _testConfigFolder);

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:testValue").Should().Be("loaded from sub folder");
        }

        [Test]
        public void ShouldLoadConfigFromSubfoldersRecursively()
        {
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, _testConfigFolder);

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:testValue").Should().Be("loaded from sub folder");
        }

        [Test]
        public void ShouldLoadJsonFilesFromAdditionalFolders()
        {
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, _testConfigFolder);

            var configuration = BuildConfiguration();

            configuration.GetValue<bool>("jsonConfig:loaded").Should().BeTrue();
            configuration.GetValue<string>("jsonConfig:format").Should().Be("json");
        }

        [Test]
        public void ShouldSupportMultipleAdditionalFolders()
        {
            var folder1 = Path.Combine(_testConfigFolder, "SubFolder");
            var paths = $"{_testConfigFolder},{folder1}";
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, paths);

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:additionalOnly").Should().Be("additionalOnly");
            configuration.GetValue<string>("testconfig:subonly").Should().Be("subonly");
        }

        [Test]
        public void ShouldSupportSemicolonSeparatedPaths()
        {
            var folder1 = Path.Combine(_testConfigFolder, "SubFolder");
            var paths = $"{_testConfigFolder};{folder1}";
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, paths);

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:additionalOnly").Should().Be("additionalOnly");
            configuration.GetValue<string>("testconfig:subonly").Should().Be("subonly");
        }

        [Test]
        public void ShouldHandleNonExistentPaths()
        {
            var nonExistentPath = "/nonexistent/folder/path";
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName,
                $"{_testConfigFolder},{nonExistentPath}");

            var act = () => { BuildConfiguration(); };
            act.Should().Throw<DirectoryNotFoundException>();
        }

        [Test]
        public void ShouldSupportRelativePaths()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var relativePath = Path.GetRelativePath(currentDir, _testConfigFolder);

            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, relativePath);

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:testValue").Should().Be("loaded from sub folder");
        }

        [Test]
        public void EnvShouldOverwriteAnyOtherConfigValues()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var relativePath = Path.GetRelativePath(currentDir, _testConfigFolder);

            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, relativePath);
            Environment.SetEnvironmentVariable(AdditionalconfigTestvalue, "loaded from env var");

            var configuration = BuildConfiguration();

            configuration.GetValue<string>("testconfig:testValue").Should().Be("loaded from env var");
        }

        [Test]
        public void ShouldReadAdditionalConfigFolderPathFromSettings()
        {
            var configuration = BuildConfiguration();

            configuration.GetValue<string>(HostConfigurator.HostAdditionalconfigpathsEnvVarName).Should()
                .Be("/anysubfolder");
            configuration.GetValue<string>("testconfig:testValue").Should().Be("loaded from sub folder");
        }

        [Test]
        public void ShouldWorkWithoutAdditionalConfigPaths()
        {
            var configuration = BuildConfiguration();
            var configurationRoot = configuration as IConfigurationRoot;

            configurationRoot.Should().NotBeNull();
            var providers = configurationRoot!.Providers.ToList();

            var orderedProviders = providers.Select(p => p.GetType()).ToList();
            orderedProviders.Should().ContainInOrder(
                typeof(CommandLineConfigurationProvider),
                typeof(YamlConfigurationProvider),
                typeof(JsonConfigurationProvider),
                typeof(EnvConfigurationProvider)
            );
        }
    }
}
