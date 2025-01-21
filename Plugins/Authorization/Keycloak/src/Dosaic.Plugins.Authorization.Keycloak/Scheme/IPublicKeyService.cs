using Microsoft.IdentityModel.Tokens;

namespace Dosaic.Plugins.Authorization.Keycloak.Scheme
{
    internal interface IPublicKeyService
    {
        Task<RsaSecurityKey> GetSecurityKey(string realm, CancellationToken cancellationToken = default);
    }
}
