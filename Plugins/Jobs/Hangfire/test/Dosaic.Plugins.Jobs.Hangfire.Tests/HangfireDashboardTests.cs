using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Middlewares;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Testing.NUnit;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    // resolving the storage publishes JobStorage.Current, which other fixtures of this project assert on
    [NonParallelizable]
    public class HangfireDashboardTests
    {
        private const string Role = "hangfire-admin";
        private const string UserHeader = "X-Test-User";
        private const string RolesHeader = "X-Test-Roles";
        private const string NotFoundMessage = "\"message\":\"Not Found\"";

        private static readonly string[] _dashboardPaths =
        [
            "/hangfire", "/hangfire/", "/hangfire/jobs/enqueued", "/hangfire/recurring", "/hangfire/js1825",
            "/hangfire/css1825", "/hangfire/does-not-exist"
        ];

        private HangfireConfiguration _configuration;
        private IHangfireConfigurator _configurator;

        [SetUp]
        public void Up()
        {
            _configuration = HangfirePluginTests.GetConfiguration();
            _configuration.Server.Enabled = false;
            _configuration.StatisticsMetrics.Enabled = false;
            _configurator = Substitute.For<IHangfireConfigurator>();
        }

        private void UseRoleFilter() =>
            _configurator.When(x => x.ConfigureDashboard(Arg.Any<DashboardOptions>())).Do(call =>
            {
                var options = call.Arg<DashboardOptions>();
                options.Authorization = [];
                options.AsyncAuthorization = [new RoleFilter(Role)];
                options.IgnoreAntiforgeryToken = true;
            });

        private HangFirePlugin GetPlugin() =>
            new(_configuration, Substitute.For<IImplementationResolver>(), [_configurator]);

        private TestDashboard BuildDashboard(Action<AuthorizationOptions> configureAuthorization = null)
        {
            var plugin = GetPlugin();
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLogging();
            services.AddRouting();
            services.AddSingleton(new DiagnosticListener("HangfireDashboardTests"));
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName,
                    _ => { });
            services.AddAuthorization(options => configureAuthorization?.Invoke(options));
            plugin.ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(serviceProvider);
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => plugin.ConfigureEndpoints(endpoints, serviceProvider));
            return new TestDashboard(app.Build(), serviceProvider);
        }

        /// <summary>
        ///     A real Kestrel host with the default <see cref="ExceptionMiddleware" /> in front of the dashboard, so
        ///     the response is checked the way a client receives it.
        /// </summary>
        private async Task<(WebApplication App, HttpClient Client)> StartKestrelDashboardAsync()
        {
            var plugin = GetPlugin();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<GlobalStatusCodeOptions>();
            builder.Services.AddSingleton(Substitute.For<IDateTimeProvider>());
            builder.Services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName,
                    _ => { });
            builder.Services.AddAuthorization();
            plugin.ConfigureServices(builder.Services);
            var app = builder.Build();
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            plugin.ConfigureEndpoints(app, app.Services);
            await app.StartAsync();
            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            return (app, client);
        }

        private static async Task<(HttpStatusCode StatusCode, string Body)> SendOverHttpAsync(HttpClient client,
            string path, HttpMethod method, string user = null, string roles = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (user is not null) request.Headers.Add(UserHeader, user);
            if (roles is not null) request.Headers.Add(RolesHeader, roles);
            if (method == HttpMethod.Post) request.Content = new FormUrlEncodedContent([]);
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        [Test]
        public async Task HiddenDashboardAnswersLikeAnUnknownPathBehindTheExceptionMiddleware()
        {
            _configuration.Dashboard.Auth = HangfireDashboardAuth.Role;
            _configuration.Dashboard.Role = Role;
            _configuration.Dashboard.HideWhenDenied = true;
            var (app, client) = await StartKestrelDashboardAsync();
            await using var _ = app;
            using var __ = client;

            foreach (var path in _dashboardPaths.Append("/hangfire/stats"))
            {
                foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
                {
                    var anonymous = await SendOverHttpAsync(client, path, method);
                    anonymous.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{method} {path} anonymous");
                    anonymous.Body.Should().Contain(NotFoundMessage, $"{method} {path} anonymous");

                    var otherRole = await SendOverHttpAsync(client, path, method, "jane", "viewer");
                    otherRole.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{method} {path} without role");
                    otherRole.Body.Should().Contain(NotFoundMessage, $"{method} {path} without role");
                }
            }

            var allowed = await SendOverHttpAsync(client, "/hangfire", HttpMethod.Get, "jane", Role);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
            allowed.Body.Should().Contain("Hangfire");

            var outside = await SendOverHttpAsync(client, "/hangfirex", HttpMethod.Get);
            outside.StatusCode.Should().Be(HttpStatusCode.NotFound);
            outside.Body.Should().Contain(NotFoundMessage);
        }

        [Test]
        public void DashboardOptionsDefaultToTheHostFilterAndPassThroughTheConfigurator()
        {
            DashboardOptions received = null;
            _configurator.When(x => x.ConfigureDashboard(Arg.Any<DashboardOptions>()))
                .Do(call => received = call.Arg<DashboardOptions>());

            var options = GetPlugin().CreateDashboardOptions();

            received.Should().BeSameAs(options);
            options.Authorization.Should().ContainSingle().Which.Should()
                .BeOfType<HangFirePlugin.HostAuthorizationFilter>();
            options.AsyncAuthorization.Should().BeEmpty();
            options.IgnoreAntiforgeryToken.Should().BeFalse();
        }

        [Test]
        public void ConfiguratorChangesWinOverTheDefaults()
        {
            UseRoleFilter();

            var options = GetPlugin().CreateDashboardOptions();

            options.Authorization.Should().BeEmpty();
            options.AsyncAuthorization.Should().ContainSingle().Which.Should().BeOfType<RoleFilter>();
            options.IgnoreAntiforgeryToken.Should().BeTrue();
        }

        [Test]
        public void HideWhenDeniedIsOffByDefault()
        {
            new HangfireConfiguration().Dashboard.HideWhenDenied.Should().BeFalse();
        }

        [Test]
        public void AuthDefaultsToHost()
        {
            var dashboard = new HangfireConfiguration().Dashboard;

            dashboard.Auth.Should().Be(HangfireDashboardAuth.Host);
            dashboard.Role.Should().BeNull();
        }

        [TestCase("role", HangfireDashboardAuth.Role)]
        [TestCase("Role", HangfireDashboardAuth.Role)]
        [TestCase("host", HangfireDashboardAuth.Host)]
        public void AuthIsBoundFromConfiguration(string value, HangfireDashboardAuth expected)
        {
            var configuration = (HangfireConfiguration)new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["hangfire:dashboard:auth"] = value,
                    ["hangfire:dashboard:role"] = "hangfire-admin"
                }).Build()
                .GetSection("hangfire", typeof(HangfireConfiguration));

            configuration.Dashboard.Auth.Should().Be(expected);
            configuration.Dashboard.Role.Should().Be("hangfire-admin");
        }

        [Test]
        public void RoleAuthUsesTheConfiguredRoleInsteadOfTheHost()
        {
            _configuration.Dashboard.Auth = HangfireDashboardAuth.Role;
            _configuration.Dashboard.Role = $" {Role} ";

            var options = GetPlugin().CreateDashboardOptions();

            options.Authorization.Should().ContainSingle().Which.Should()
                .BeOfType<HangFirePlugin.RoleAuthorizationFilter>().Which.Role.Should().Be(Role);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void RoleAuthWithoutRoleStopsTheStart(string role)
        {
            _configuration.Dashboard.Auth = HangfireDashboardAuth.Role;
            _configuration.Dashboard.Role = role;

            var action = () => GetPlugin().CreateDashboardOptions();

            action.Should().Throw<InvalidOperationException>().WithMessage("*hangfire:dashboard:role*");
        }

        [Test]
        public async Task RoleAuthOpensTheDashboardForTheRoleOnAnyHost()
        {
            _configuration.Dashboard.Auth = HangfireDashboardAuth.Role;
            _configuration.Dashboard.Role = Role;
            _configuration.AllowedDashboardHost = "";
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            var allowed = await dashboard.SendAsync("/hangfire", host: "public.example.com", user: "jane", roles: Role);
            var otherRole = await dashboard.SendAsync("/hangfire", host: "localhost", user: "jane", roles: "viewer");
            var anonymous = await dashboard.SendAsync("/hangfire/stats", "POST", host: "localhost");

            allowed.StatusCode.Should().Be(StatusCodes.Status200OK);
            otherRole.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            anonymous.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Test]
        public async Task RoleAuthAnswers401And403WhenNotHidden()
        {
            _configuration.Dashboard.Auth = HangfireDashboardAuth.Role;
            _configuration.Dashboard.Role = Role;
            var dashboard = BuildDashboard();

            var anonymous = await dashboard.SendAsync("/hangfire");
            var otherRole = await dashboard.SendAsync("/hangfire", user: "jane", roles: "viewer");

            anonymous.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
            otherRole.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Test]
        public void RoleFilterRefusesAnUnauthenticatedPrincipalWithTheRoleClaim()
        {
            var filter = new HangFirePlugin.RoleAuthorizationFilter(Role);
            var httpContext = new DefaultHttpContext
            {
                RequestServices = TestingDefaults.ServiceProvider(),
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, Role)]))
            };

            filter.Authorize(new AspNetCoreDashboardContext(new global::Hangfire.MemoryStorage.MemoryStorage(),
                new DashboardOptions(), httpContext)).Should().BeFalse();
        }

        [Test]
        public async Task DefaultDashboardIsOpenForTheAllowedHost()
        {
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfire");

            response.StatusCode.Should().Be(StatusCodes.Status200OK);
            response.Body.Should().Contain("Hangfire");
        }

        [Test]
        public async Task DefaultDashboardAnswers401ForAnotherHost()
        {
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfire", host: "example.com");

            response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public async Task DefaultDashboardAnswers401ForEveryHostWhenNoHostIsConfigured()
        {
            _configuration.AllowedDashboardHost = "";
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfire");

            response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public async Task DeniedUserGets403WhenHideWhenDeniedIsOff()
        {
            UseRoleFilter();
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfire", user: "jane", roles: "viewer");

            response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Test]
        public async Task UserWithTheRoleOpensTheDashboard()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            var page = await dashboard.SendAsync("/hangfire", host: "public.example.com", user: "jane", roles: Role);
            var stats = await dashboard.SendAsync("/hangfire/stats", "POST", user: "jane", roles: Role);

            page.StatusCode.Should().Be(StatusCodes.Status200OK);
            page.Body.Should().Contain("Hangfire");
            stats.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Test]
        public async Task UserWithoutTheRoleGets404OnEveryDashboardPath()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            foreach (var path in _dashboardPaths)
            {
                var response = await dashboard.SendAsync(path, user: "jane", roles: "viewer");
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound, path);
                response.Body.Should().BeEmpty(path);
            }

            var post = await dashboard.SendAsync("/hangfire/stats", "POST", user: "jane", roles: "viewer");
            post.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            post.Body.Should().BeEmpty();
        }

        [Test]
        public async Task RequestWithoutTokenGets404OnEveryDashboardPath()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            foreach (var path in _dashboardPaths)
            {
                var response = await dashboard.SendAsync(path);
                response.StatusCode.Should().Be(StatusCodes.Status404NotFound, path);
                response.Body.Should().BeEmpty(path);
            }

            var post = await dashboard.SendAsync("/hangfire/recurring/trigger", "POST");
            post.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Test]
        public async Task HideWhenDeniedAlsoHidesTheDefaultHostFilter()
        {
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            var denied = await dashboard.SendAsync("/hangfire/jobs/enqueued", host: "example.com");
            var allowed = await dashboard.SendAsync("/hangfire/jobs/enqueued");

            denied.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            allowed.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Test]
        public async Task FallbackPolicyDoesNotChallengeTheDashboard()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            var response = await dashboard.SendAsync("/hangfire");

            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        [Test]
        public async Task DashboardWorksBelowAPathBase()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfire", user: "jane", roles: Role,
                pathBase: "/p/my-app/api");

            response.StatusCode.Should().Be(StatusCodes.Status200OK);
            response.Body.Should().Contain("/p/my-app/api/hangfire");
        }

        [Test]
        public async Task PathsOutsideTheDashboardAreNotTouched()
        {
            UseRoleFilter();
            _configuration.Dashboard.HideWhenDenied = true;
            var dashboard = BuildDashboard();

            var response = await dashboard.SendAsync("/hangfirex");

            response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        private sealed record TestDashboard(RequestDelegate Pipeline, IServiceProvider Services)
        {
            public async Task<(int StatusCode, string Body)> SendAsync(string path, string method = "GET",
                string host = "localhost", string user = null, string roles = null, string pathBase = null)
            {
                var context = new DefaultHttpContext();
                context.Request.Method = method;
                context.Request.Scheme = "http";
                context.Request.Host = new HostString(host);
                context.Request.Path = path;
                if (pathBase is not null) context.Request.PathBase = pathBase;
                if (user is not null) context.Request.Headers[UserHeader] = user;
                if (roles is not null) context.Request.Headers[RolesHeader] = roles;
                if (method == "POST")
                {
                    context.Request.ContentType = "application/x-www-form-urlencoded";
                    context.Request.Body = new MemoryStream();
                }

                var body = new MemoryStream();
                context.Response.Body = body;
                await using var scope = Services.CreateAsyncScope();
                context.RequestServices = scope.ServiceProvider;
                await Pipeline(context);
                return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()));
            }
        }

        private sealed class RoleFilter(string role) : IDashboardAsyncAuthorizationFilter
        {
            public Task<bool> AuthorizeAsync(DashboardContext context) =>
                Task.FromResult(context.GetHttpContext().User.IsInRole(role));
        }

        private sealed class TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var user = Request.Headers[UserHeader].ToString();
                if (string.IsNullOrEmpty(user)) return Task.FromResult(AuthenticateResult.NoResult());
                var claims = new List<Claim> { new(ClaimTypes.Name, user) };
                claims.AddRange(Request.Headers[RolesHeader].ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => new Claim(ClaimTypes.Role, x)));
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
            }
        }
    }
}
