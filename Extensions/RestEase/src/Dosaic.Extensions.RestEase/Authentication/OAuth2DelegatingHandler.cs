using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Dosaic.Extensions.RestEase.Authentication
{
    public sealed class OAuth2DelegatingHandler : DelegatingHandler
    {
        private readonly ITokenProvider _tokenProvider;

        public OAuth2DelegatingHandler(ITokenProvider tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var hadAuth = request.Headers.Authorization != null;
            if (!hadAuth)
                await Apply(request, false, cancellationToken).ConfigureAwait(false);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized || hadAuth)
                return response;

            response.Dispose();
            _tokenProvider.Invalidate();
            await Apply(request, true, cancellationToken).ConfigureAwait(false);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task Apply(HttpRequestMessage request, bool forceRefresh, CancellationToken cancellationToken)
        {
            var token = await _tokenProvider.GetTokenAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Value);
        }
    }
}
