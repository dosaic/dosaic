using System.Net;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Dosaic.Hosting.Abstractions.Middlewares
{
    /// <summary>
    /// Configured by UseKestrel(options => options.Limits.MaxRequestBodySize = ?)
    /// </summary>
    [Middleware]
    public sealed class RequestContentLengthLimitMiddleware : ApiMiddleware
    {
        private readonly ILogger _logger;

        public RequestContentLengthLimitMiddleware(RequestDelegate next,
            ILogger<RequestContentLengthLimitMiddleware> logger, IDateTimeProvider dateTimeProvider) :
            base(next, dateTimeProvider)
        {
            _logger = logger;
        }

        public override Task Invoke(HttpContext context)
        {
            var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize;
            if (context.Request.Method is WebRequestMethods.Http.Post or WebRequestMethods.Http.Put &&
                context.Request.ContentLength > limit)
            {
                _logger.LogDebug("Request payload is too large (limit:{RequestBodyLimit})", limit);
                return WriteDefaultResponse(context, StatusCodes.Status413PayloadTooLarge);
            }

            return Next.Invoke(context);
        }
    }
}
