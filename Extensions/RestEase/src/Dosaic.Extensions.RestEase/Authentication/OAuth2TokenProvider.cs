using System.Text.Json;
using Dosaic.Extensions.RestEase.Json;
using RestEase;

namespace Dosaic.Extensions.RestEase.Authentication
{
    public sealed class OAuth2TokenProvider : ITokenProvider, IDisposable
    {
        private static readonly Dictionary<GrantType, string> _grantTypeMapping = new()
        {
            { GrantType.Password, "password" },
            { GrantType.ClientCredentials, "client_credentials" }
        };

        private readonly AuthenticationConfig _config;
        private readonly IAuthApi _api;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private OAuth2Model _cache;

        internal OAuth2TokenProvider(AuthenticationConfig config, IAuthApi api)
        {
            _config = config;
            _api = api;
        }

        public static OAuth2TokenProvider Create(AuthenticationConfig config, JsonSerializerOptions jsonOptions = null)
        {
            var options = jsonOptions ?? RestEaseDefaults.CreateDefaultJsonOptions();
            var client = new RestClient(config.BaseUrl)
            {
                RequestBodySerializer = new SystemTextJsonRequestBodySerializer(options),
                ResponseDeserializer = new SystemTextJsonResponseDeserializer(options)
            };
            return new OAuth2TokenProvider(config, client.For<IAuthApi>());
        }

        public async Task<AccessToken> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            var current = _cache;
            if (!forceRefresh && current != null && !current.ShouldCreateToken(_config.RefreshSkew) && !current.ShouldRefreshToken(_config.RefreshSkew))
                return Map(current);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                current = _cache;
                if (!forceRefresh && current != null && !current.ShouldCreateToken(_config.RefreshSkew) && !current.ShouldRefreshToken(_config.RefreshSkew))
                    return Map(current);

                OAuth2Model fresh;
                if (forceRefresh || current is null || current.ShouldCreateToken(_config.RefreshSkew))
                    fresh = await GetToken(cancellationToken).ConfigureAwait(false);
                else
                    fresh = await RefreshToken(current.RefreshToken, cancellationToken).ConfigureAwait(false);

                fresh.Created = DateTime.UtcNow;
                _cache = fresh;
                return Map(fresh);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Invalidate() => _cache = null;

        private Task<OAuth2Model> GetToken(CancellationToken cancellationToken)
        {
            var data = new Dictionary<string, object> { { "grant_type", _grantTypeMapping[_config.GrantType] } };
            if (!string.IsNullOrEmpty(_config.ClientId)) data["client_id"] = _config.ClientId;
            if (!string.IsNullOrEmpty(_config.ClientSecret)) data["client_secret"] = _config.ClientSecret;
            if (!string.IsNullOrEmpty(_config.Username)) data["username"] = _config.Username;
            if (!string.IsNullOrEmpty(_config.Password)) data["password"] = _config.Password;
            if (!string.IsNullOrEmpty(_config.Scope)) data["scope"] = _config.Scope;
            if (!string.IsNullOrEmpty(_config.Audience)) data["audience"] = _config.Audience;
            return _api.GetToken(_config.TokenUrlPath, data, cancellationToken);
        }

        private Task<OAuth2Model> RefreshToken(string refreshToken, CancellationToken cancellationToken)
        {
            var data = new Dictionary<string, object>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            };
            if (!string.IsNullOrEmpty(_config.ClientId)) data["client_id"] = _config.ClientId;
            if (!string.IsNullOrEmpty(_config.ClientSecret)) data["client_secret"] = _config.ClientSecret;
            return _api.GetToken(_config.TokenUrlPath, data, cancellationToken);
        }

        private static AccessToken Map(OAuth2Model model) => new()
        {
            TokenType = string.IsNullOrEmpty(model.TokenType) ? "Bearer" : model.TokenType,
            Value = model.AccessToken,
            ExpiresAt = new DateTimeOffset(model.Created.AddSeconds(model.ExpiresIn), TimeSpan.Zero)
        };

        public void Dispose() => _gate.Dispose();
    }
}
