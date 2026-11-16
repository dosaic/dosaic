using IdentityModel.AspNetCore.OAuth2Introspection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols.Configuration;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    internal static class ZitadelTokenRetrieval
    {
        /// <summary>
        ///     The <c>Authorization: Bearer</c> header, and when the request has no <c>Authorization</c> header at all
        ///     and its path lies below one of <see cref="ZitadelCookieTokenConfiguration.Paths" />, the cookie.
        ///     The path restriction is a CSRF guard: a browser sends the cookie on its own, so it must never
        ///     authenticate an API that changes state.
        /// </summary>
        public static Func<HttpRequest, string> Create(ZitadelCookieTokenConfiguration cookieToken)
        {
            var fromHeader = TokenRetrieval.FromAuthorizationHeader();
            if (string.IsNullOrWhiteSpace(cookieToken?.Name)) return fromHeader;
            var cookieName = cookieToken.Name.Trim();
            var paths = NormalizePaths(cookieToken.Paths);
            return request =>
            {
                if (!StringValues.IsNullOrEmpty(request.Headers.Authorization)) return fromHeader(request);
                if (!paths.Any(path => request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
                    return null;
                return request.Cookies.TryGetValue(cookieName, out var token) && !string.IsNullOrWhiteSpace(token)
                    ? token
                    : null;
            };
        }

        private static PathString[] NormalizePaths(string[] paths)
        {
            var result = (paths ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => "/" + x.Trim().Trim('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (result.Length == 0)
                throw new InvalidConfigurationException(
                    "Configuration: zitadel:cookieToken:paths must name at least one path when zitadel:cookieToken:name is set.");
            if (result.Contains("/"))
                throw new InvalidConfigurationException(
                    "Configuration: zitadel:cookieToken:paths must not contain '/', the cookie would authenticate every path.");
            return result.Select(x => new PathString(x)).ToArray();
        }
    }
}
