using System.Collections.Immutable;
using System.Net.Http;
using Amazon.Runtime;
using Amazon.S3;
using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Extensions;
using HealthChecks.Uris;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Storage;
using NSubstitute;
using NUnit.Framework;
using FileType = Dosaic.Plugins.Persistence.S3.File.FileType;

namespace Dosaic.Plugins.Persistence.S3.Tests
{
    [TestFixture]
    public class S3FileStoragePluginTests
    {
        private readonly S3Configuration _configuration = new()
        {
            Endpoint = "s3.endpoint.de",
            AccessKey = "access",
            SecretKey = "secret",
            Region = "region",
            UseSsl = true
        };

        private S3FileStoragePlugin _plugin = null!;

        [SetUp]
        public void Init()
        {
            _plugin = new S3FileStoragePlugin(_configuration);
        }

        [Test]
        public void PluginConfiguresServices()
        {
            var sc = TestingDefaults.ServiceCollection();
            _plugin.ConfigureServices(sc);
            sc.AddFileStorage<SampleBucket>();
            sc.AddSingleton<S3Configuration>();
            var sp = sc.BuildServiceProvider();

            var client = sp.GetRequiredService<IAmazonS3>();
            client.Should().NotBeNull();
            var s3Config = client.Config.Should().BeOfType<AmazonS3Config>().Subject;
            s3Config.ServiceURL.Should().Be($"https://{_configuration.Endpoint}/");
            s3Config.AuthenticationRegion.Should().Be(_configuration.Region);
            s3Config.ForcePathStyle.Should().BeTrue();
            s3Config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
            s3Config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
            var credentials = _plugin.GetCredentials().GetCredentials();
            credentials.AccessKey.Should().Be(_configuration.AccessKey);
            credentials.SecretKey.Should().Be(_configuration.SecretKey);

            sp.GetRequiredService<IFileTypeDefinitionResolver>().Should().NotBeNull();
            sp.GetRequiredService<IFileStorage>().Should().NotBeNull();
            var fileStorage = sp.GetRequiredService<IFileStorage>() as FileStorage;
            fileStorage!.GetDefinitions(FileType.All).Should().HaveCountGreaterThan(1);

            var fileStorageSampleBucket = sp.GetRequiredService<IFileStorage<SampleBucket>>();
            fileStorageSampleBucket.Should().NotBeNull();
            fileStorageSampleBucket.Should().BeOfType<FileStorage<SampleBucket>>();
        }

        [Test]
        public void PluginUsesDefaultCredentialChainWithoutAccessKey()
        {
            var plugin = new S3FileStoragePlugin(new S3Configuration { Endpoint = "s3.endpoint.de" });

            plugin.GetCredentials().Should().BeNull();
        }

        [Test]
        public void PluginFallsBackToDefaultRegionAndPlainHttp()
        {
            var plugin = new S3FileStoragePlugin(new S3Configuration { Endpoint = "localhost:9000" });

            var s3Config = plugin.GetS3Client().Config.Should().BeOfType<AmazonS3Config>().Subject;
            s3Config.ServiceURL.Should().Be("http://localhost:9000/");
            s3Config.AuthenticationRegion.Should().Be(S3Configuration.DefaultRegion);
        }

        [Test]
        public void PluginUsesEndpointWithSchemeAsIs()
        {
            var plugin = new S3FileStoragePlugin(new S3Configuration { Endpoint = "https://s3.endpoint.de:9000" });

            var s3Config = plugin.GetS3Client().Config.Should().BeOfType<AmazonS3Config>().Subject;
            s3Config.ServiceURL.Should().Be("https://s3.endpoint.de:9000/");
        }

        [Test]
        public void PluginRespectsForcePathStyleAndChecksumSettings()
        {
            var plugin = new S3FileStoragePlugin(new S3Configuration
            {
                Endpoint = "s3.endpoint.de",
                ForcePathStyle = false,
                UseChecksums = true
            });

            var s3Config = plugin.GetS3Client().Config.Should().BeOfType<AmazonS3Config>().Subject;
            s3Config.ForcePathStyle.Should().BeFalse();
            s3Config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_SUPPORTED);
            s3Config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_SUPPORTED);
        }

        [Test]
        public void IContentInspectorCanBeCustomized()
        {
            var sc = TestingDefaults.ServiceCollection();
            _plugin.ConfigureServices(sc);

            sc.Should().Contain(d => d.ServiceType == typeof(IContentInspector));

            sc.Replace(ServiceDescriptor.Singleton<IContentInspector>(sp =>
                new ContentInspectorBuilder
                {
                    Definitions = DefaultDefinitions.All()
                            .Where(x => x.File.Extensions.Contains("pdf")).ToList()
                }
                    .Build()));

            var sp = sc.BuildServiceProvider();

            var contentInspector = sp.GetRequiredService<IContentInspector>();
            var matchers = contentInspector.GetInaccessibleValue("DefinitionMatchers");
            matchers.GetType().GetProperty("Length")!.GetValue(matchers).Should().Be(1);
        }

        [Test]
        public void IFileTypeDefinitionResolverCanBeCustomized()
        {
            var sc = TestingDefaults.ServiceCollection();
            _plugin.ConfigureServices(sc);
            sc.AddSingleton<S3Configuration>();

            sc.Should().Contain(d => d.ServiceType == typeof(IFileTypeDefinitionResolver));

            sc.Replace(ServiceDescriptor.Singleton<IFileTypeDefinitionResolver>(sp =>
                new EmptyFileTypeDefinitionResolver()));

            var sp = sc.BuildServiceProvider();

            var typeDefinitionResolver = sp.GetRequiredService<IFileTypeDefinitionResolver>();
            typeDefinitionResolver.GetDefinitions(FileType.All).Should().BeEmpty();

            var fileStorage = sp.GetRequiredService<IFileStorage>() as FileStorage;
            fileStorage!.GetDefinitions(FileType.All).Should().BeEmpty();
        }

        [Test]
        public void PluginConfiguresServicesWithLocalFileSystem()
        {
            var localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var localConfig = new S3Configuration { UseLocalFileSystem = true, LocalFileSystemPath = localPath };
                var localPlugin = new S3FileStoragePlugin(localConfig);
                var sc = TestingDefaults.ServiceCollection();
                localPlugin.ConfigureServices(sc);
                sc.AddFileStorage<SampleBucket>();
                var sp = sc.BuildServiceProvider();

                sp.GetRequiredService<IFileStorage>().Should().BeOfType<LocalFileSystemBlobStorage>();
                sp.GetService<IAmazonS3>().Should().BeNull();
                sp.GetRequiredService<IFileTypeDefinitionResolver>().Should().NotBeNull();
                sp.GetRequiredService<IFileStorage<SampleBucket>>().Should().BeOfType<FileStorage<SampleBucket>>();
            }
            finally
            {
                if (Directory.Exists(localPath)) Directory.Delete(localPath, true);
            }
        }

        [Test]
        public void PluginConfiguresHealthChecksWithLocalFileSystem()
        {
            var localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var localConfig = new S3Configuration { UseLocalFileSystem = true, LocalFileSystemPath = localPath };
                var localPlugin = new S3FileStoragePlugin(localConfig);
                var healthChecksBuilder = Substitute.For<IHealthChecksBuilder>();
                healthChecksBuilder.Services.Returns(new ServiceCollection());
                localPlugin.ConfigureHealthChecks(healthChecksBuilder);

                healthChecksBuilder.Received(1)
                    .Add(Arg.Is<HealthCheckRegistration>(h => h.Name == "s3-local-filesystem"
                                                            && h.FailureStatus == HealthStatus.Unhealthy));
                var registration = healthChecksBuilder.ReceivedCalls().Last().GetArguments()![0] as HealthCheckRegistration;
                registration.Should().NotBeNull();
                var healthCheck = registration!.Factory.Invoke(new ServiceCollection().BuildServiceProvider());
                var result = healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None).GetAwaiter().GetResult();
                result.Status.Should().Be(HealthStatus.Healthy);
            }
            finally
            {
                if (Directory.Exists(localPath)) Directory.Delete(localPath, true);
            }
        }

        [Test]
        public void PluginConfiguresHealthChecks()
        {
            var healthChecksBuilder = Substitute.For<IHealthChecksBuilder>();
            healthChecksBuilder.Services.Returns(new ServiceCollection());
            _plugin.ConfigureHealthChecks(healthChecksBuilder);
            AssertUriHealthCheck(healthChecksBuilder);
        }

        [Test]
        public void PluginConfiguresHealthChecksWithPath()
        {
            var healthChecksBuilder = Substitute.For<IHealthChecksBuilder>();
            healthChecksBuilder.Services.Returns(new ServiceCollection());
            _configuration.HealthCheckPath = "/health/live";
            _plugin.ConfigureHealthChecks(healthChecksBuilder);
            AssertUriHealthCheck(healthChecksBuilder);
        }

        private void AssertUriHealthCheck(IHealthChecksBuilder healthChecksBuilder)
        {
            healthChecksBuilder.Received(1)
                .Add(Arg.Is<HealthCheckRegistration>(h => h.Name == "s3"));
            var registration = healthChecksBuilder.ReceivedCalls().Last().GetArguments()![0] as HealthCheckRegistration;
            registration.Should().NotBeNull();
            var sp = new ServiceCollection()
                .AddSingleton(Substitute.For<IHttpClientFactory>())
                .BuildServiceProvider();
            var healthCheck = registration!.Factory.Invoke(sp);
            var uriHealthCheck = healthCheck.Should().BeOfType<UriHealthCheck>().Subject;
            uriHealthCheck.Should().NotBeNull();
            var options = uriHealthCheck.GetInaccessibleValue<UriHealthCheckOptions>("_options");
            options.Should().NotBeNull();
            var uriOptions = options.GetInaccessibleValue<List<UriOptions>>("UrisOptions");
            uriOptions.Should().NotBeNull().And.HaveCount(1);
            var uriOption = uriOptions.Single();
            uriOption.Uri.Authority.Should().Be(_configuration.Endpoint);
            uriOption.Uri.AbsolutePath.Should().Be("/" + _configuration.HealthCheckPath.TrimStart('/'));
        }
    }

    internal class EmptyFileTypeDefinitionResolver : IFileTypeDefinitionResolver
    {
        public ImmutableArray<Definition> GetDefinitions(FileType fileType)
        {
            return ImmutableArray<Definition>.Empty;
        }
    }
}
