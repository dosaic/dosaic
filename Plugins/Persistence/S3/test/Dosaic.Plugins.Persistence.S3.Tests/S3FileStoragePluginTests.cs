using System.Net.Http;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit;
using Dosaic.Testing.NUnit.Extensions;
using FluentAssertions;
using HealthChecks.Uris;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using NSubstitute;
using NUnit.Framework;

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

            var fileStorage = sp.GetRequiredService<IFileStorage<SampleBucket>>();
            fileStorage.Should().NotBeNull();
            fileStorage.Should().BeOfType<FileStorage<SampleBucket>>();
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
}
