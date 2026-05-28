using System.Net.Http;
using System.Net.Http.Headers;

namespace Dosaic.Extensions.RestEase.Handlers
{
    public sealed class UserAgentHandler : DelegatingHandler
    {
        private readonly ProductInfoHeaderValue _value;

        public UserAgentHandler(string userAgent)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
                _value = ProductInfoHeaderValue.Parse(userAgent);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_value != null && !request.Headers.UserAgent.Contains(_value))
                request.Headers.UserAgent.Add(_value);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
