using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.InteropServices;
using Google.Protobuf.WellKnownTypes;
using Zitadel.Api;
using Zitadel.Credentials;
using Zitadel.User.V2;
using Type = Zitadel.User.V2.Type;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    public interface IManagementService
    {
        Task<List<ListServiceAccountResultItem>> ListServiceAccountsAsync();

        Task<ServiceAccountCreationResult> CreateServiceAccountAsync(string userid, string name, Timestamp keyExpirationTime,
            string description = "");

        Task<string> GetBearerTokenForServiceAccountAsync(string jsonString);

        Task<string> CreateUserAccountAsync(string userid, string email,
            string displayName, string givenName, string familyName, string password);
    }

    public record ServiceAccountCreationResult(string UserId, string KeyId, string KeyContent);

    public record ListServiceAccountResultItem(
        string UserId,
        string State,
        string Name,
        string Description,
        bool HasSecret,
        string Owner,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public class ManagementService(ZitadelConfiguration config) : IManagementService
    {
        [ExcludeFromCodeCoverage(Justification = "No interface to mock zitadel client")]
        public async Task<List<ListServiceAccountResultItem>> ListServiceAccountsAsync()
        {
            var client = Clients.UserService(GetClientOptions());
            var users = await client.ListUsersAsync(new ListUsersRequest
            {
                Queries = { new SearchQuery { TypeQuery = new TypeQuery { Type = Type.Machine } } }
            });
            return users.Result.Select(x =>
                new ListServiceAccountResultItem(x.UserId, x.State.ToString(), x.Machine.Name, x.Machine.Description,
                    x.Machine.HasSecret, x.Details.ResourceOwner, x.Details.CreationDate.ToDateTime(),
                    x.Details.ChangeDate.ToDateTime()
                )).ToList();
        }

        [ExcludeFromCodeCoverage(Justification = "No interface to mock zitadel client")]
        public async Task<ServiceAccountCreationResult> CreateServiceAccountAsync(string userid, string name, Timestamp keyExpirationTime,
            string description = "")
        {
            var client = Clients.UserService(GetClientOptions());
            var result =
                await client.CreateUserAsync(PrepareCreateServiceAccountRequest(userid, name));

            var (key, keyContent) = await CreateServiceAccountKeyAsync(result.Id, keyExpirationTime);

            return new ServiceAccountCreationResult(result.Id, key.KeyId, keyContent);
        }

        [ExcludeFromCodeCoverage(Justification = "No interface to mock zitadel client")]

        public async Task<(AddKeyResponse key, string keyContent)> CreateServiceAccountKeyAsync(string userId, Timestamp timestamp)
        {
            var client = Clients.UserService(GetClientOptions());
            var key = await client.AddKeyAsync(new AddKeyRequest
            {
                UserId = userId,
                ExpirationDate = timestamp
            });

            var keyContent = await ReadKeyContent(key);
            return (key, keyContent);
        }

        internal static async Task<string> ReadKeyContent(AddKeyResponse key)
        {
            // https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-5.0#read-binary-payloads
            ByteArrayContent content;
            if (MemoryMarshal.TryGetArray(key.KeyContent.Memory, out var segment))
            {
                // Success. Use the ByteString's underlying array.
                content = new ByteArrayContent(segment.Array!, segment.Offset, segment.Count);
            }
            else
            {
                // TryGetArray didn't succeed. Fall back to creating a copy of the data with ToByteArray.
                content = new ByteArrayContent(key.KeyContent.ToByteArray());
            }

            var keyContent = await content.ReadAsStringAsync();
            return keyContent;
        }

        internal CreateUserRequest PrepareCreateServiceAccountRequest(string userid, string name)
        {
            return new()
            {
                OrganizationId = config.OrganizationId,
                Machine = new CreateUserRequest.Types.Machine { Name = name, Description = name },
                UserId = userid,
            };
        }

        [ExcludeFromCodeCoverage(Justification = "No interface to mock zitadel client")]
        public async Task<string> CreateUserAccountAsync(string userid, string email,
            string displayName, string givenName, string familyName, string password)
        {
            var client = Clients.UserService(GetClientOptions());
            var result =
                await client.CreateUserAsync(PrepareCreateUserRequest(userid, email, displayName, givenName, familyName,
                    password));

            return result.Id;
        }

        internal CreateUserRequest PrepareCreateUserRequest(string userid, string email, string displayName,
            string givenName, string familyName, string password)
        {
            return new()
            {
                OrganizationId = config.OrganizationId,
                Human = new CreateUserRequest.Types.Human()
                {
                    Profile = new SetHumanProfile
                    {
                        DisplayName = displayName,
                        GivenName = givenName,
                        FamilyName = familyName
                    },
                    Email = new SetHumanEmail { Email = email, IsVerified = true },
                    Password = new Password { Password_ = password }
                },
                UserId = userid
            };
        }

        [ExcludeFromCodeCoverage(Justification = "No interface to mock zitadel client")]
        public async Task<string> GetBearerTokenForServiceAccountAsync(string jsonString)
        {
            var authOptions = new ServiceAccount.AuthOptions();
            authOptions.AdditionalScopes.Add($"urn:zitadel:iam:org:project:id:{config.ProjectId}:aud");
            authOptions.AdditionalScopes.Add("offline_access");
            authOptions.AdditionalScopes.Add("profile");
            authOptions.AdditionalScopes.Add("email");
            var token = await ServiceAccount.LoadFromJsonString(config.ServiceAccount)
                .AuthenticateAsync(config.Host, authOptions);
            return token;
        }

        internal Clients.Options GetClientOptions()
        {
            var serviceAccount = ITokenProvider.ServiceAccount(
                config.Host,
                ServiceAccount.LoadFromJsonString(config.ServiceAccount),
                new ServiceAccount.AuthOptions { ApiAccess = true });
            return new Clients.Options(config.Host, serviceAccount);
        }
    }
}
