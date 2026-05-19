using System.Net.Http;
using Polly;

namespace Dosaic.Extensions.RestEase.Handlers
{
    public sealed class ResilienceDelegatingHandler : DelegatingHandler
    {
        private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

        public ResilienceDelegatingHandler(ResiliencePipeline<HttpResponseMessage> pipeline)
        {
            _pipeline = pipeline;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _pipeline.ExecuteAsync(ct => new ValueTask<HttpResponseMessage>(base.SendAsync(request, ct)), cancellationToken).AsTask();
        }
    }
}
