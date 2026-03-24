using AwesomeAssertions;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    [Explicit]
    [TestFixture]
    public class ManagementServiceIntegrationTests
    {
        private readonly ZitadelConfiguration _config = new()
        {
            ProjectId = "1",
            OrganizationId = "2",
            Host = "https://zitadel.example.com",
            JwtProfile = "{}",
            ServiceAccount = "{}"
        };

        [Explicit]
        [Test]
        public async Task CreateServiceAccount()
        {
            var managementService = new ManagementService(_config);
            var result = await managementService.CreateServiceAccountAsync("test-sa", "Test Service Account",
                Timestamp.FromDateTime(DateTime.UtcNow.AddYears(10)));
            result.Should().NotBeNull();
        }

        [Explicit]
        [Test]
        public async Task CreateUser()
        {
            var managementService = new ManagementService(_config);
            var persona = new Persona("test.user", "test@example.com", "Test", "User");
            var result = await managementService.CreateUserAccountAsync(persona.UserId, persona.Email,
                persona.DisplayName,
                persona.GivenName, persona.FamilyName, persona.Password);
            result.Should().NotBeNull();
        }

        private record Persona
        {
            public Persona(string UserId, string Email, string GivenName, string FamilyName
            )
            {
                this.UserId = UserId;
                this.Email = Email;
                DisplayName = UserId;
                this.GivenName = GivenName;
                this.FamilyName = FamilyName;
                // password policy: at least 8 characters, one uppercase, one lowercase, one digit
                Password = char.ToUpper(UserId[0]) + UserId.Substring(1) + "1";
            }

            public string UserId { get; }
            public string Email { get; }
            public string DisplayName { get; }
            public string GivenName { get; }
            public string FamilyName { get; }
            public string Password { get; }
        }
    }
}
