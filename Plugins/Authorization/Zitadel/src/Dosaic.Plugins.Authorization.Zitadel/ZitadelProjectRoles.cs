using System.Security.Claims;
using System.Text.Json;
using Zitadel.Authentication;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    internal static class ZitadelProjectRoles
    {
        public static string ProjectScopedClaimType(string projectId) => $"urn:zitadel:iam:org:project:{projectId}:roles";

        /// <summary>
        ///     Reads the role names (the keys of the JSON object) of <c>urn:zitadel:iam:org:project:roles</c> and of
        ///     <c>urn:zitadel:iam:org:project:&lt;projectId&gt;:roles</c> and adds a <see cref="ClaimTypes.Role" />
        ///     claim for every role the principal does not have yet. Roles of other projects are not mapped.
        /// </summary>
        public static void MapToRoleClaims(ClaimsPrincipal principal, string projectId)
        {
            if (principal is null) return;
            var claimTypes = new HashSet<string>(StringComparer.Ordinal) { ZitadelClaimTypes.Role };
            if (!string.IsNullOrWhiteSpace(projectId)) claimTypes.Add(ProjectScopedClaimType(projectId));
            var roles = principal.Claims
                .Where(x => claimTypes.Contains(x.Type))
                .SelectMany(x => ReadRoleNames(x.Value))
                .Distinct(StringComparer.Ordinal)
                .Where(x => !principal.HasClaim(ClaimTypes.Role, x))
                .Select(x => new Claim(ClaimTypes.Role, x))
                .ToList();
            if (roles.Count == 0) return;
            principal.AddIdentity(new ClaimsIdentity(roles, ZitadelDefaults.AuthenticationScheme, ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private static List<string> ReadRoleNames(string value)
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                return document.RootElement.ValueKind == JsonValueKind.Object
                    ? document.RootElement.EnumerateObject().Select(x => x.Name).Where(x => x.Length > 0).ToList()
                    : [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
