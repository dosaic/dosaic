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

            HttpRequestMessage retryRequest = null;
            if (!hadAuth)
                retryRequest = await CloneAsync(request, cancellationToken).ConfigureAwait(false);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized || hadAuth)
            {
                retryRequest?.Dispose();
                return response;
            }

            response.Dispose();
            _tokenProvider.Invalidate();
            await Apply(retryRequest, true, cancellationToken).ConfigureAwait(false);
            return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
        }

        private async Task Apply(HttpRequestMessage request, bool forceRefresh, CancellationToken cancellationToken)
        {
            var token = await _tokenProvider.GetTokenAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Value);
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            if (request.Content != null)
            {
                var buffer = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var bufferedOriginal = new ByteArrayContent(buffer);
                foreach (var header in request.Content.Headers)
                    bufferedOriginal.Headers.TryAddWithoutValidation(header.Key, header.Value);

                var clonedContent = new ByteArrayContent(buffer);
                foreach (var header in request.Content.Headers)
                    clonedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

                request.Content = bufferedOriginal;
                clone.Content = clonedContent;
            }

            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            foreach (var prop in request.Options)
                ((IDictionary<string, object>)clone.Options)[prop.Key] = prop.Value;

            return clone;
        }
    }
}
