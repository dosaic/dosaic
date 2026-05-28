using System.Text.Json.Serialization;

namespace Dosaic.Extensions.RestEase.Authentication
{
    internal class OAuth2Model
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }

        [JsonIgnore]
        public DateTime Created { get; set; } = DateTime.UtcNow.AddSeconds(-1);

        public bool ShouldCreateToken(TimeSpan skew) => IsExpired(skew) && IsRefreshExpired(skew);
        public bool ShouldRefreshToken(TimeSpan skew) => IsExpired(skew) && !IsRefreshExpired(skew);
        private bool IsExpired(TimeSpan skew) => Created.AddSeconds(ExpiresIn) - skew < DateTime.UtcNow;
        private bool IsRefreshExpired(TimeSpan skew) => RefreshExpiresIn <= 0 || Created.AddSeconds(RefreshExpiresIn) - skew < DateTime.UtcNow;
    }
}
