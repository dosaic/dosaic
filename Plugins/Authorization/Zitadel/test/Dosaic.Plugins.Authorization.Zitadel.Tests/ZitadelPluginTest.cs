using AwesomeAssertions;
using Dosaic.Testing.NUnit.Assertions;
using IdentityModel.AspNetCore.OAuth2Introspection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.Configuration;
using NUnit.Framework;
using Zitadel.Authentication;
using AuthenticationFailedContext = IdentityModel.AspNetCore.OAuth2Introspection.AuthenticationFailedContext;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    [TestFixture]
    public class ZitadelPluginTest
    {
        private readonly FakeLogger<ZitadelPlugin> _logger = new();

        private ZitadelPlugin GetPlugin(Action<ZitadelConfiguration> configure = null)
        {
            var config = new ZitadelConfiguration
            {
                Host = "http://localhost:8080",
                ProjectId = "1",
                OrganizationId = "3",
                JwtProfile = "{}",
                ServiceAccount = "{}",
                UseHttps = false,
                ValidateEndpoints = false,
                ValidateIssuer = false,
            };
            configure?.Invoke(config);
            return new ZitadelPlugin(config, _logger);
        }

        [Test]
        public void ZitadelPluginSetsUpServices()
        {
            var sc = new ServiceCollection().AddDistributedMemoryCache();
            var plugin = GetPlugin(o =>
            {
                o.Host = "localhost:8080";
                o.UseHttps = false;
                o.ValidateEndpoints = false;
                o.ValidateIssuer = false;
            });
            plugin.ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            sp.GetService<OAuth2IntrospectionHandler>().Should().NotBeNull();
            sp.GetService<IOptions<AuthenticationOptions>>()?.Value?.DefaultScheme.Should()
                .Be(ZitadelDefaults.AuthenticationScheme);
            var opts =
                sp.GetService<IOptionsSnapshot<OAuth2IntrospectionOptions>>()!.Get(ZitadelDefaults
                    .AuthenticationScheme);
            opts.Authority.Should().Be("http://localhost:8080");
            opts.EnableCaching.Should().BeTrue();
            opts.CacheDuration.Should().Be(TimeSpan.FromMinutes(1));
            opts.CacheKeyPrefix.Should().Be("ZITADEL_");
            opts.DiscoveryPolicy.RequireHttps.Should().Be(false);
            opts.DiscoveryPolicy.ValidateEndpoints.Should().Be(false);
            opts.DiscoveryPolicy.ValidateIssuerName.Should().Be(false);
        }

        [Test]
        public void ZitadelPluginSetsUpServicesAndSetProps()
        {
            var sc = new ServiceCollection().AddDistributedMemoryCache();
            var plugin = GetPlugin(o =>
            {
                o.Host = "localhost:8081";
                o.UseHttps = true;
                o.ValidateEndpoints = true;
                o.ValidateIssuer = true;
            });
            plugin.ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            sp.GetService<OAuth2IntrospectionHandler>().Should().NotBeNull();
            sp.GetService<IOptions<AuthenticationOptions>>()?.Value?.DefaultScheme.Should()
                .Be(ZitadelDefaults.AuthenticationScheme);
            var opts =
                sp.GetService<IOptionsSnapshot<OAuth2IntrospectionOptions>>()!.Get(ZitadelDefaults
                    .AuthenticationScheme);
            opts.Authority.Should().Be("https://localhost:8081");
            opts.EnableCaching.Should().BeTrue();
            opts.CacheDuration.Should().Be(TimeSpan.FromMinutes(1));
            opts.CacheKeyPrefix.Should().Be("ZITADEL_");
            opts.DiscoveryPolicy.RequireHttps.Should().Be(true);
            opts.DiscoveryPolicy.ValidateEndpoints.Should().Be(true);
            opts.DiscoveryPolicy.ValidateIssuerName.Should().Be(true);
        }

        [Test]
        public void ZitadelPluginThrowsOnInvalidConfig()
        {
            var sc = new ServiceCollection().AddDistributedMemoryCache();
            var plugin = GetPlugin(o =>
            {
                o.Host = "http://localhost:8081";
                o.UseHttps = true;
                o.ValidateEndpoints = true;
                o.ValidateIssuer = true;
            });
            plugin.ConfigureServices(sc);
            var sp = sc.BuildServiceProvider();
            var opts = sp.GetService<IOptionsSnapshot<OAuth2IntrospectionOptions>>()!;
            opts.Invoking(x => x.Get(ZitadelDefaults.AuthenticationScheme)).Should()
                .Throw<InvalidConfigurationException>();
        }

        [Test]
        public async Task OnAuthenticationFailedIsLogged()
        {
            var plugin = GetPlugin();
            var context = new AuthenticationFailedContext(new DefaultHttpContext(),
                new AuthenticationScheme(ZitadelDefaults.AuthenticationScheme, "", typeof(OpenIdConnectHandler)),
                new OAuth2IntrospectionOptions()) { Error = "Test error" };

            await plugin.OnAuthenticationFailed(context);
            // Check if the log contains the expected message
            // This is a bit tricky since we are using a fake logger, you would need to implement a way to capture logs in the FakeLogger and assert on them here.
            _logger.Entries.Should().Contain(x => x.Message == "Test error");
        }
    }
}
