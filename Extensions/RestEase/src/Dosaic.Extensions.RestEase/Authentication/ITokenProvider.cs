namespace Dosaic.Extensions.RestEase.Authentication
{
    public interface ITokenProvider
    {
        Task<AccessToken> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
        void Invalidate();
    }

    public sealed class AccessToken
    {
        public string TokenType { get; init; }
        public string Value { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}
