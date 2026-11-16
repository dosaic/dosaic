using System.Security.Claims;
using AwesomeAssertions;
using Dosaic.Testing.NUnit.Assertions;
using Dosaic.Testing.NUnit.Extensions;
using IdentityModel.AspNetCore.OAuth2Introspection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.Configuration;
using NSubstitute;
using NUnit.Framework;
using Zitadel.Authentication;
using AuthenticationFailedContext = IdentityModel.AspNetCore.OAuth2Introspection.AuthenticationFailedContext;
using TokenValidatedContext = IdentityModel.AspNetCore.OAuth2Introspection.TokenValidatedContext;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    [TestFixture]
    public class ZitadelPluginTest
    {
        private readonly FakeLogger<ZitadelPlugin> _logger = new();

        private ZitadelPlugin GetPlugin(Action<ZitadelConfiguration> configure = null, IZitadelConfigurator[] configurators = null)
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
            return new ZitadelPlugin(config, _logger, configurators ?? []);
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
        public void ConfigureApplicationWorks()
        {
            var appBuilder = Substitute.For<IApplicationBuilder>();
            var sc = new ServiceCollection();
            sc.AddAuthentication();
            sc.AddAuthorization();
            var sp = sc.BuildServiceProvider();
            appBuilder.ApplicationServices.Returns(sp);
            GetPlugin().ConfigureApplication(appBuilder);
            var useCalls = appBuilder.GetReceivedMiddlewareCalls();
            useCalls[0].AssertMiddleware<AuthenticationMiddleware>();
            useCalls[1].AssertMiddleware<AuthorizationMiddleware>();
        }

        [Test]
        public void ConfigureApplicationCallsConfiguratorsInOrder()
        {
            var appBuilder = Substitute.For<IApplicationBuilder>();
            var sc = new ServiceCollection();
            sc.AddAuthentication();
            sc.AddAuthorization();
            var sp = sc.BuildServiceProvider();
            appBuilder.ApplicationServices.Returns(sp);
            var callOrder = new List<string>();
            var configurator = Substitute.For<IZitadelConfigurator>();
            configurator.When(x => x.ConfigureApplicationBeforeAuthentication(Arg.Any<IApplicationBuilder>()))
                .Do(_ => callOrder.Add("BeforeAuthentication"));
            configurator.When(x => x.ConfigureApplicationAfterAuthentication(Arg.Any<IApplicationBuilder>()))
                .Do(_ => callOrder.Add("AfterAuthentication"));
            configurator.When(x => x.ConfigureApplicationBeforeAuthorization(Arg.Any<IApplicationBuilder>()))
                .Do(_ => callOrder.Add("BeforeAuthorization"));
            configurator.When(x => x.ConfigureApplicationAfterAuthorization(Arg.Any<IApplicationBuilder>()))
                .Do(_ => callOrder.Add("AfterAuthorization"));
            GetPlugin(configurators: [configurator]).ConfigureApplication(appBuilder);
            callOrder.Should().BeEquivalentTo(
                ["BeforeAuthentication", "AfterAuthentication", "BeforeAuthorization", "AfterAuthorization"],
                opts => opts.WithStrictOrdering());
            var useCalls = appBuilder.GetReceivedMiddlewareCalls();
            useCalls[0].AssertMiddleware<AuthenticationMiddleware>();
            useCalls[1].AssertMiddleware<AuthorizationMiddleware>();
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
                new OAuth2IntrospectionOptions())
            { Error = "Test error" };

            await plugin.OnAuthenticationFailed(context);
            // Check if the log contains the expected message
            // This is a bit tricky since we are using a fake logger, you would need to implement a way to capture logs in the FakeLogger and assert on them here.
            _logger.Entries.Should().Contain(x => x.Message == "Test error");
        }

        private static IServiceProvider BuildServices(ZitadelPlugin plugin)
        {
            var sc = new ServiceCollection().AddDistributedMemoryCache();
            sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            sc.AddLogging();
            plugin.ConfigureServices(sc);
            return sc.BuildServiceProvider();
        }

        private static OAuth2IntrospectionOptions GetOptions(IServiceProvider sp) =>
            sp.GetRequiredService<IOptionsMonitor<OAuth2IntrospectionOptions>>().Get(ZitadelDefaults.AuthenticationScheme);

        private static void ConfigureCookie(ZitadelConfiguration config) =>
            config.CookieToken = new ZitadelCookieTokenConfiguration { Name = "hangfire-token", Paths = ["/hangfire"] };

        private static HttpContext RequestWithCookie(IServiceProvider sp, string path, string method = "GET")
        {
            var context = new DefaultHttpContext { RequestServices = sp };
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.Headers.Cookie = "hangfire-token=cookie-token";
            return context;
        }

        private static async Task<ClaimsPrincipal> ValidateTokenAsync(IServiceProvider sp, params Claim[] claims)
        {
            var options = GetOptions(sp);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ZitadelDefaults.AuthenticationScheme,
                options.NameClaimType, options.RoleClaimType));
            var context = new TokenValidatedContext(new DefaultHttpContext { RequestServices = sp },
                new AuthenticationScheme(ZitadelDefaults.AuthenticationScheme, null, typeof(OAuth2IntrospectionHandler)),
                options)
            { Principal = principal, SecurityToken = "token" };
            await options.Events.TokenValidated(context);
            return context.Principal;
        }

        [Test]
        public void TokenIsReadFromTheBearerHeaderOnlyByDefault()
        {
            var sp = BuildServices(GetPlugin());

            var retriever = GetOptions(sp).TokenRetriever;

            retriever(RequestWithCookie(sp, "/hangfire").Request).Should().BeNull();
        }

        [Test]
        public void CookieTokenIsReadOnTheConfiguredPathOnly()
        {
            var sp = BuildServices(GetPlugin(ConfigureCookie));

            var retriever = GetOptions(sp).TokenRetriever;

            retriever(RequestWithCookie(sp, "/hangfire/stats").Request).Should().Be("cookie-token");
            retriever(RequestWithCookie(sp, "/graphql", "POST").Request).Should().BeNull();
        }

        [Test]
        public async Task GraphQlPostWithOnlyTheCookieStaysUnauthorized()
        {
            var sp = BuildServices(GetPlugin(ConfigureCookie));
            var context = RequestWithCookie(sp, "/graphql", "POST");

            var result = await context.AuthenticateAsync(ZitadelDefaults.AuthenticationScheme);
            await context.ChallengeAsync(ZitadelDefaults.AuthenticationScheme);

            result.Succeeded.Should().BeFalse();
            result.None.Should().BeTrue();
            context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public void BrokenCookieConfigurationStopsTheStart()
        {
            var plugin = GetPlugin(o => o.CookieToken = new ZitadelCookieTokenConfiguration { Name = "hangfire-token" });

            plugin.Invoking(x => x.ConfigureServices(new ServiceCollection())).Should()
                .Throw<InvalidConfigurationException>();
        }

        [Test]
        public async Task ProjectScopedRolesAreMappedWhenEnabled()
        {
            var sp = BuildServices(GetPlugin(o => o.MapProjectRoles = true));

            var principal = await ValidateTokenAsync(sp,
                new Claim("urn:zitadel:iam:org:project:1:roles", """{"hangfire-admin":{"3":"org.example.com"}}"""),
                new Claim("urn:zitadel:iam:org:project:2:roles", """{"foreign":{"3":"org.example.com"}}"""));

            principal.IsInRole("hangfire-admin").Should().BeTrue();
            principal.IsInRole("foreign").Should().BeFalse();
        }

        [Test]
        public async Task GenericProjectRolesAreMappedOnceWhenEnabled()
        {
            var sp = BuildServices(GetPlugin(o => o.MapProjectRoles = true));

            var principal = await ValidateTokenAsync(sp,
                new Claim(ZitadelClaimTypes.Role, """{"hangfire-admin":{"3":"org.example.com"}}"""));

            principal.IsInRole("hangfire-admin").Should().BeTrue();
            principal.FindAll(ClaimTypes.Role).Should().ContainSingle(x => x.Value == "hangfire-admin");
            // the Zitadel library's own handler still runs before the mapping
            principal.HasClaim("urn:zitadel:iam:org:3:project:roles", "hangfire-admin").Should().BeTrue();
        }

        [Test]
        public async Task ProjectScopedRolesAreNotMappedByDefault()
        {
            var sp = BuildServices(GetPlugin());

            var principal = await ValidateTokenAsync(sp,
                new Claim("urn:zitadel:iam:org:project:1:roles", """{"hangfire-admin":{"3":"org.example.com"}}"""));

            principal.IsInRole("hangfire-admin").Should().BeFalse();
        }
    }
}
