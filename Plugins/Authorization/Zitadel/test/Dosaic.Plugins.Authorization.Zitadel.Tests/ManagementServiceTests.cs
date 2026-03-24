using AwesomeAssertions;
using Google.Protobuf;
using NUnit.Framework;
using Zitadel.User.V2;

namespace Dosaic.Plugins.Authorization.Zitadel.Tests
{
    [TestFixture]
    public class ManagementServiceTests
    {
        private ManagementService _managementService = null!;
        private ZitadelConfiguration _config = null!;

        [SetUp]
        public void SetUp()
        {
            _config = new ZitadelConfiguration
            {
                ProjectId = "test-project-id",
                OrganizationId = "test-org-id",
                Host = "https://zitadel.example.com",
                JwtProfile = "{}",
                ServiceAccount =
                    "{\"type\":\"serviceaccount\",\"keyId\":\"key123\",\"key\":\"-----BEGIN RSA PRIVATE KEY-----\\nMIIEpAIBAAKCAQEA...\\n-----END RSA PRIVATE KEY-----\",\"userId\":\"user123\"}"
            };
            _managementService = new ManagementService(_config);
        }

        [Test]
        public void PrepareCreateServiceAccountRequest_SetsCorrectProperties()
        {
            var userid = "sa-user-123";
            var name = "TestServiceAccount";

            var result = _managementService.PrepareCreateServiceAccountRequest(userid, name);

            result.OrganizationId.Should().Be(_config.OrganizationId);
            result.UserId.Should().Be(userid);
            result.Machine.Should().NotBeNull();
            result.Machine.Name.Should().Be(name);
            result.Machine.Description.Should().Be(name);
        }

        [Test]
        public void PrepareCreateServiceAccountRequest_UsesNameAsDescription()
        {
            var userid = "sa-user-456";
            var name = "AnotherAccount";

            var result = _managementService.PrepareCreateServiceAccountRequest(userid, name);

            result.Machine.Description.Should().Be(name);
        }

        [Test]
        public void PrepareCreateUserRequest_SetsCorrectProperties()
        {
            var userid = "user-123";
            var email = "test@example.com";
            var displayName = "Test User";
            var givenName = "Test";
            var familyName = "User";
            var password = "SecurePassword123!";

            var result =
                _managementService.PrepareCreateUserRequest(userid, email, displayName, givenName, familyName,
                    password);

            result.OrganizationId.Should().Be(_config.OrganizationId);
            result.UserId.Should().Be(userid);
            result.Human.Should().NotBeNull();
            result.Human.Profile.DisplayName.Should().Be(displayName);
            result.Human.Profile.GivenName.Should().Be(givenName);
            result.Human.Profile.FamilyName.Should().Be(familyName);
            result.Human.Email.Email.Should().Be(email);
            result.Human.Email.IsVerified.Should().BeTrue();
            result.Human.Password.Password_.Should().Be(password);
        }

        [Test]
        public void PrepareCreateUserRequest_SetsEmailAsVerified()
        {
            var result =
                _managementService.PrepareCreateUserRequest("id", "mail@test.com", "Display", "First", "Last", "pass");

            result.Human.Email.IsVerified.Should().BeTrue();
        }

        [Test]
        public void PrepareCreateUserRequest_WithSpecialCharacters_SetsCorrectProperties()
        {
            var userid = "user-special-öäü";
            var email = "special+chars@example.com";
            var displayName = "Ünïcödé Üser";
            var givenName = "Ünïcödé";
            var familyName = "Üser";
            var password = "P@$$w0rd!#%&";

            var result =
                _managementService.PrepareCreateUserRequest(userid, email, displayName, givenName, familyName,
                    password);

            result.UserId.Should().Be(userid);
            result.Human.Profile.DisplayName.Should().Be(displayName);
            result.Human.Profile.GivenName.Should().Be(givenName);
            result.Human.Profile.FamilyName.Should().Be(familyName);
            result.Human.Email.Email.Should().Be(email);
            result.Human.Password.Password_.Should().Be(password);
        }

        [Test]
        public async Task ReadKeyContent_ReturnsKeyContentAsString()
        {
            var expectedContent = "{\"keyId\":\"key-123\",\"key\":\"some-private-key\"}";
            var keyResponse = new AddKeyResponse
            {
                KeyId = "key-123",
                KeyContent = ByteString.CopyFromUtf8(expectedContent)
            };

            var result = await ManagementService.ReadKeyContent(keyResponse);

            result.Should().Be(expectedContent);
        }

        [Test]
        public async Task ReadKeyContent_WithEmptyContent_ReturnsEmptyString()
        {
            var keyResponse = new AddKeyResponse { KeyId = "key-empty", KeyContent = ByteString.CopyFromUtf8("") };

            var result = await ManagementService.ReadKeyContent(keyResponse);

            result.Should().BeEmpty();
        }

        [Test]
        public async Task ReadKeyContent_WithLargeContent_ReturnsCorrectString()
        {
            var largeContent = new string('x', 10_000);
            var keyResponse = new AddKeyResponse
            {
                KeyId = "key-large",
                KeyContent = ByteString.CopyFromUtf8(largeContent)
            };

            var result = await ManagementService.ReadKeyContent(keyResponse);

            result.Should().Be(largeContent);
        }

        [Test]
        public async Task ReadKeyContent_WithUtf8Content_ReturnsCorrectString()
        {
            var utf8Content = "{\"key\":\"value with ünïcödé\"}";
            var keyResponse =
                new AddKeyResponse { KeyId = "key-utf8", KeyContent = ByteString.CopyFromUtf8(utf8Content) };

            var result = await ManagementService.ReadKeyContent(keyResponse);

            result.Should().Be(utf8Content);
        }

        [Test]
        public void GetClientOptions_ReturnsOptionsWithCorrectHost()
        {
            var result = _managementService.GetClientOptions();

            result.Endpoint.Should().Be(_config.Host);
        }

        [Test]
        public void PrepareCreateServiceAccountRequest_DoesNotSetHuman()
        {
            var result = _managementService.PrepareCreateServiceAccountRequest("sa-id", "sa-name");

            result.Human.Should().BeNull();
        }

        [Test]
        public void PrepareCreateUserRequest_DoesNotSetMachine()
        {
            var result = _managementService.PrepareCreateUserRequest("uid", "e@e.com", "D", "G", "F", "p");

            result.Machine.Should().BeNull();
        }
    }
}
