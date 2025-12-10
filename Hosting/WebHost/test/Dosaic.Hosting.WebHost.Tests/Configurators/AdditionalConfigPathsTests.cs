using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NetEscapades.Configuration.Yaml;
using NUnit.Framework;

namespace Dosaic.Hosting.WebHost.Tests.Configurators
{
    public class AdditionalConfigPathsTests
    {
        private string _testConfigFolder = null!;

        [SetUp]
        public void Setup()
        {
            _testConfigFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestConfigFolder");
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", null);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", null);
        }

        [Test]
        public void ShouldLoadConfigFromAdditionalFolderViaEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", _testConfigFolder);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("additionalConfig:testValue").Should().Be("loaded from additional folder");
            configuration.GetValue<bool>("additionalConfig:featureFlag").Should().BeTrue();
        }

        [Test]
        public void ShouldLoadConfigFromSubfoldersRecursively()
        {
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", _testConfigFolder);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("subfolder:config").Should().Be("loaded from subfolder");
            configuration.GetValue<int>("subfolder:level").Should().Be(2);
        }

        [Test]
        public void ShouldLoadJsonFilesFromAdditionalFolders()
        {
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", _testConfigFolder);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<bool>("jsonConfig:loaded").Should().BeTrue();
            configuration.GetValue<string>("jsonConfig:format").Should().Be("json");
        }

        [Test]
        public void ShouldSupportMultipleAdditionalFolders()
        {
            var folder1 = Path.Combine(_testConfigFolder, "SubFolder");
            var paths = $"{_testConfigFolder},{folder1}";
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", paths);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("additionalConfig:testValue").Should().Be("loaded from additional folder");
            configuration.GetValue<string>("subfolder:config").Should().Be("loaded from subfolder");
        }

        [Test]
        public void ShouldSupportSemicolonSeparatedPaths()
        {
            var folder1 = Path.Combine(_testConfigFolder, "SubFolder");
            var paths = $"{_testConfigFolder};{folder1}";
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", paths);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("additionalConfig:testValue").Should().Be("loaded from additional folder");
            configuration.GetValue<string>("subfolder:config").Should().Be("loaded from subfolder");
        }

        [Test]
        public void ShouldHandleNonExistentPaths()
        {
            var nonExistentPath = "/nonexistent/folder/path";
            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", $"{_testConfigFolder},{nonExistentPath}");

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("additionalConfig:testValue").Should().Be("loaded from additional folder");
        }

        [Test]
        public void ShouldSupportRelativePaths()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var relativePath = Path.GetRelativePath(currentDir, _testConfigFolder);

            Environment.SetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS", relativePath);

            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configuration = webApplication.Configuration;

            configuration.GetValue<string>("additionalConfig:testValue").Should().Be("loaded from additional folder");
        }

        [Test]
        public void ShouldWorkWithoutAdditionalConfigPaths()
        {
            var pluginWebHostBuilder = PluginWebHostBuilder.Create([typeof(TypeImplementationResolverTests.UnitTestPluginConfig)]);
            var host = pluginWebHostBuilder.Build();

            var webApplication = host.As<WebApplication>();
            var configurationProviders = webApplication.Configuration.As<IConfigurationRoot>().Providers.ToList();

            configurationProviders[0].Should().BeOfType<YamlConfigurationProvider>();
            configurationProviders[^1].Should().BeOfType<EnvConfigurationProvider>();
        }
    }
}

