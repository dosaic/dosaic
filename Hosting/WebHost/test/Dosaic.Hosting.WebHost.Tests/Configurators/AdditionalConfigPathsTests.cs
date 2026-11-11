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
        public void SchemaFileInTheWorkingDirectoryIsNotLoadedAsConfiguration()
        {
            var schema = Path.Combine(Environment.CurrentDirectory, "appsettings.schema.json");
            Services.ConfigurationSchemaCommand.TryRun([typeof(HostConfiguration)],
                ["--write-config-schema", schema], TextWriter.Null);
            try
            {
                var configuration = BuildConfiguration();

                configuration["$schema"].Should().BeNull();
                configuration["title"].Should().BeNull();
                configuration.GetSection("properties").Exists().Should().BeFalse();
                configuration.As<IConfigurationRoot>().Providers.OfType<JsonConfigurationProvider>()
                    .Should().NotContain(x => x.Source.Path!.EndsWith(".schema.json"));
            }
            finally
            {
                File.Delete(schema);
            }
        }

        [Test]
        public void SchemaFileInAnAdditionalConfigPathIsNotLoadedAsConfiguration()
        {
            var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Services.ConfigurationSchemaCommand.TryRun([typeof(HostConfiguration)],
                ["--write-config-schema", Path.Combine(folder, "appsettings.schema.json")], TextWriter.Null);
            File.WriteAllText(Path.Combine(folder, "appsettings.extra.json"), """{ "extra": { "value": "loaded" } }""");
            Environment.SetEnvironmentVariable(HostConfigurator.HostAdditionalconfigpathsEnvVarName, folder);
            try
            {
                var configuration = BuildConfiguration();

                configuration["extra:value"].Should().Be("loaded");
                configuration["$schema"].Should().BeNull();
                configuration.GetSection("properties").Exists().Should().BeFalse();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestCase("appsettings.schema.json", true)]
        [TestCase("appsettings.Schema.JSON", true)]
        [TestCase("/config/appsettings.schema.json", true)]
        [TestCase("appsettings.json", false)]
        [TestCase("appsettings.schema.yml", false)]
        [TestCase("appsettings.schemas.json", false)]
        public void OnlyFilesEndingInSchemaJsonCountAsSchemaFiles(string file, bool expected)
        {
            HostConfigurator.IsSchemaFile(file).Should().Be(expected);
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
