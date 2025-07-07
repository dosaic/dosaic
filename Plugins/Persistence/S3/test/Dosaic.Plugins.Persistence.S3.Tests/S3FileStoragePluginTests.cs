using System.Collections.Immutable;
using System.Net.Http;
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
using Minio;
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

            var client = sp.GetRequiredService<IMinioClient>();
            client.Should().NotBeNull();
            var baseUrl = client.Config.BaseUrl;
            baseUrl.Should().NotBeNull();
            baseUrl.Should().Be(_configuration.Endpoint);
            var accessKey = client.Config.AccessKey;
            accessKey.Should().NotBeNull();
            accessKey.Should().Be(_configuration.AccessKey);
            var secretKey = client.Config.SecretKey;
            secretKey.Should().NotBeNull();
            secretKey.Should().Be(_configuration.SecretKey);
            var region = client.Config.Region;
            region.Should().NotBeNull();
            region.Should().Be(_configuration.Region);
            var secure = client.Config.Secure;
            secure.Should().Be(_configuration.UseSsl);

            sp.GetRequiredService<IFileTypeDefinitionResolver>().Should().NotBeNull();
            sp.GetRequiredService<IFileStorage>().Should().NotBeNull();
            var fileStorage = sp.GetRequiredService<IFileStorage>() as FileStorage;
            fileStorage!.GetDefinitions(FileType.All).Should().HaveCount(65);

            var fileStorageSampleBucket = sp.GetRequiredService<IFileStorage<SampleBucket>>();
            fileStorageSampleBucket.Should().NotBeNull();
            fileStorageSampleBucket.Should().BeOfType<FileStorage<SampleBucket>>();
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
            _configuration.HealthCheckPath = "/minio/health/live";
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
