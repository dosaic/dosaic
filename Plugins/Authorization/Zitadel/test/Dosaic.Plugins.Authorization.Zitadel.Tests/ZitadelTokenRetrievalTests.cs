using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.Configuration;
using NUnit.Framework;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    public class ZitadelTokenRetrievalTests
    {
        private const string CookieName = "hangfire-token";

        private static readonly ZitadelCookieTokenConfiguration _cookieToken = new()
        {
            Name = CookieName,
            Paths = ["/hangfire"]
        };

        private static HttpRequest Request(string path, string cookie = null, string authorization = null,
            string pathBase = null, string method = "GET")
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            if (pathBase is not null) context.Request.PathBase = pathBase;
            if (cookie is not null) context.Request.Headers.Cookie = $"{CookieName}={cookie}";
            if (authorization is not null) context.Request.Headers.Authorization = authorization;
            return context.Request;
        }

        [Test]
        public void WithoutCookieConfigurationOnlyTheBearerHeaderCounts()
        {
            var retriever = ZitadelTokenRetrieval.Create(null);

            retriever(Request("/hangfire", cookie: "cookie-token")).Should().BeNull();
            retriever(Request("/hangfire", authorization: "Bearer header-token")).Should().Be("header-token");
        }

        [Test]
        public void EmptyCookieNameSwitchesTheCookieOff()
        {
            var retriever = ZitadelTokenRetrieval.Create(new ZitadelCookieTokenConfiguration { Paths = ["/hangfire"] });

            retriever(Request("/hangfire", cookie: "cookie-token")).Should().BeNull();
        }

        [TestCase("/hangfire")]
        [TestCase("/hangfire/")]
        [TestCase("/hangfire/stats")]
        [TestCase("/Hangfire/jobs/enqueued")]
        public void CookieIsReadOnTheConfiguredPaths(string path)
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request(path, cookie: "cookie-token")).Should().Be("cookie-token");
        }

        [Test]
        public void PathIsMatchedAfterThePathBase()
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request("/hangfire/stats", cookie: "cookie-token", pathBase: "/p/my-app/api"))
                .Should().Be("cookie-token");
        }

        [TestCase("/graphql", "POST")]
        [TestCase("/api/items", "GET")]
        [TestCase("/hangfirex", "GET")]
        [TestCase("/", "GET")]
        [TestCase("/api/hangfire", "GET")]
        public void CookieIsIgnoredOnEveryOtherPath(string path, string method)
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request(path, cookie: "cookie-token", method: method)).Should().BeNull();
        }

        [Test]
        public void BearerHeaderWinsOverTheCookie()
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request("/hangfire", cookie: "cookie-token", authorization: "Bearer header-token"))
                .Should().Be("header-token");
        }

        [Test]
        public void AnyAuthorizationHeaderTurnsTheCookieOff()
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request("/hangfire", cookie: "cookie-token", authorization: "Basic dXNlcjpwYXNz"))
                .Should().BeNull();
        }

        [Test]
        public void MissingOrEmptyCookieGivesNoToken()
        {
            var retriever = ZitadelTokenRetrieval.Create(_cookieToken);

            retriever(Request("/hangfire")).Should().BeNull();
            retriever(Request("/hangfire", cookie: "")).Should().BeNull();
        }

        [Test]
        public void PathsAreNormalized()
        {
            var retriever = ZitadelTokenRetrieval.Create(new ZitadelCookieTokenConfiguration
            {
                Name = $" {CookieName} ",
                Paths = [" hangfire/ ", "", null]
            });

            retriever(Request("/hangfire/stats", cookie: "cookie-token")).Should().Be("cookie-token");
            retriever(Request("/graphql", cookie: "cookie-token")).Should().BeNull();
        }

        [Test]
        public void CookieWithoutPathsIsRefused()
        {
            var action = () => ZitadelTokenRetrieval.Create(new ZitadelCookieTokenConfiguration { Name = CookieName });

            action.Should().Throw<InvalidConfigurationException>().WithMessage("*zitadel:cookieToken:paths*");
        }

        [TestCase("/")]
        [TestCase("//")]
        public void RootPathIsRefused(string path)
        {
            var action = () => ZitadelTokenRetrieval.Create(new ZitadelCookieTokenConfiguration
            {
                Name = CookieName,
                Paths = ["/hangfire", path]
            });

            action.Should().Throw<InvalidConfigurationException>().WithMessage("*must not contain '/'*");
        }
    }
}
