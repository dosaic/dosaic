using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    [Configuration("Zitadel")]
    public class ZitadelConfiguration
    {
        public required string ProjectId { get; set; }
        public required string OrganizationId { get; set; }
        public required string Host { get; set; }
        public bool UseHttps { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
        public bool ValidateEndpoints { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationInMinutes { get; set; } = 1;
        public string CacheKeyPrefix { get; set; } = "ZITADEL_";
        public required string JwtProfile { get; set; }
        public string ServiceAccount { get; set; }

        /// <summary>
        ///     Lets the "ZITADEL" scheme read the access token from a cookie on the configured paths only.
        ///     Null (default) reads the token from the <c>Authorization: Bearer</c> header alone.
        /// </summary>
        public ZitadelCookieTokenConfiguration CookieToken { get; set; }

        /// <summary>
        ///     Adds one <see cref="System.Security.Claims.ClaimTypes.Role" /> claim per role of the project roles
        ///     claims <c>urn:zitadel:iam:org:project:roles</c> and <c>urn:zitadel:iam:org:project:&lt;projectId&gt;:roles</c>
        ///     (<see cref="ProjectId" /> only), so <c>User.IsInRole("role")</c> and <c>RequireRole("role")</c> work for both.
        /// </summary>
        public bool MapProjectRoles { get; set; }
    }

    public class ZitadelCookieTokenConfiguration
    {
        /// <summary>Name of the cookie that holds the access token. Empty (default) switches the cookie off.</summary>
        public string Name { get; set; }

        /// <summary>
        ///     Request paths (after <c>PathBase</c>) on which the cookie is read, matched per path segment:
        ///     <c>/hangfire</c> matches <c>/hangfire</c> and <c>/hangfire/stats</c>, not <c>/hangfirex</c>. On every
        ///     other path the cookie is ignored, so it never authenticates the API. Required when <see cref="Name" />
        ///     is set; <c>/</c> is refused because it would cover every path.
        /// </summary>
        public string[] Paths { get; set; } = [];
    }
}
