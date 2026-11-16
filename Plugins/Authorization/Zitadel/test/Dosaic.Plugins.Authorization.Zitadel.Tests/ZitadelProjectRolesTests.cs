using System.Security.Claims;
using AwesomeAssertions;
using NUnit.Framework;
using Zitadel.Authentication;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    public class ZitadelProjectRolesTests
    {
        private const string ProjectId = "123";

        private static ClaimsPrincipal Principal(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, ZitadelDefaults.AuthenticationScheme, "name", ZitadelClaimTypes.Role));

        [Test]
        public void RolesOfTheProjectRolesClaimBecomeRoleClaims()
        {
            var principal = Principal(new Claim(ZitadelClaimTypes.Role,
                """{"hangfire-admin":{"1":"org.example.com"},"viewer":{"1":"org.example.com"}}"""));

            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);

            principal.IsInRole("hangfire-admin").Should().BeTrue();
            principal.IsInRole("viewer").Should().BeTrue();
            principal.FindAll(ClaimTypes.Role).Select(x => x.Value).Should()
                .BeEquivalentTo("hangfire-admin", "viewer");
        }

        [Test]
        public void RolesOfTheProjectScopedClaimBecomeRoleClaims()
        {
            var principal = Principal(new Claim("urn:zitadel:iam:org:project:123:roles",
                """{"hangfire-admin":{"1":"org.example.com"}}"""));

            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);

            principal.IsInRole("hangfire-admin").Should().BeTrue();
        }

        [Test]
        public void RolesOfOtherProjectsAreNotMapped()
        {
            var principal = Principal(new Claim("urn:zitadel:iam:org:project:999:roles",
                """{"hangfire-admin":{"1":"org.example.com"}}"""));

            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);

            principal.IsInRole("hangfire-admin").Should().BeFalse();
            principal.Identities.Should().ContainSingle();
        }

        [Test]
        public void RolesThePrincipalAlreadyHasAreNotAddedTwice()
        {
            var principal = Principal(
                new Claim(ZitadelClaimTypes.Role, """{"hangfire-admin":{}}"""),
                new Claim("urn:zitadel:iam:org:project:123:roles", """{"hangfire-admin":{},"ops":{}}"""));
            principal.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Role, "hangfire-admin")], "other"));

            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);
            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);

            principal.FindAll(ClaimTypes.Role).Select(x => x.Value).Should().BeEquivalentTo("hangfire-admin", "ops");
        }

        [TestCase("not json")]
        [TestCase("[\"admin\"]")]
        [TestCase("\"admin\"")]
        [TestCase("{}")]
        public void ClaimsThatAreNoRoleObjectAddNothing(string value)
        {
            var principal = Principal(new Claim(ZitadelClaimTypes.Role, value));

            ZitadelProjectRoles.MapToRoleClaims(principal, ProjectId);

            principal.Identities.Should().ContainSingle();
        }

        [Test]
        public void WithoutProjectIdOnlyTheGenericClaimIsMapped()
        {
            var principal = Principal(
                new Claim(ZitadelClaimTypes.Role, """{"viewer":{}}"""),
                new Claim("urn:zitadel:iam:org:project::roles", """{"admin":{}}"""));

            ZitadelProjectRoles.MapToRoleClaims(principal, " ");

            principal.IsInRole("viewer").Should().BeTrue();
            principal.IsInRole("admin").Should().BeFalse();
        }

        [Test]
        public void NullPrincipalIsIgnored()
        {
            var action = () => ZitadelProjectRoles.MapToRoleClaims(null, ProjectId);

            action.Should().NotThrow();
        }
    }
}
