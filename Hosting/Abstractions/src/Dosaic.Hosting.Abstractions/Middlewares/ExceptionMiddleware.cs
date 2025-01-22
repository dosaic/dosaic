using System.Diagnostics;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Hosting.Abstractions.Middlewares.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dosaic.Hosting.Abstractions.Middlewares
{
    [Middleware(int.MinValue)]
    public sealed class ExceptionMiddleware : ApiMiddleware
    {
        private readonly ILogger _logger;
        private readonly GlobalStatusCodeOptions _globalStatusCodeOptions;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, GlobalStatusCodeOptions globalStatusCodeOptions, IDateTimeProvider dateTimeProvider) : base(next, dateTimeProvider)
        {
            _logger = logger;
            _globalStatusCodeOptions = globalStatusCodeOptions;
        }

        public override async Task Invoke(HttpContext context)
        {
            try
            {
                await Next.Invoke(context).ConfigureAwait(false);
                if (_globalStatusCodeOptions.DefaultStatusCodes.Contains(context.Response.StatusCode))
                    await WriteDefaultResponse(context, context.Response.StatusCode);
            }
            catch (ValidationDosaicException validationDosaicException)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, validationDosaicException.Message);
                _logger.LogInformation(validationDosaicException, "Validation Dosaic Exception occured");
                var validationErrorResponse = new ValidationErrorResponse(DateTimeProvider.UtcNow,
                    validationDosaicException.Message, context.TraceIdentifier,
                    validationDosaicException.ValidationErrors);
                await WriteResponse(context, validationDosaicException.HttpStatus, validationErrorResponse);
            }
            catch (DosaicException dosaicException)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, dosaicException.Message);
                _logger.LogInformation(dosaicException, "Dosaic Exception occured");
                await WriteDefaultResponse(context, dosaicException.HttpStatus, dosaicException.Message);
            }
            catch (Exception e)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, e.Message);
                _logger.LogError(e, "Unhandled Exception occured");
                await WriteDefaultResponse(context, StatusCodes.Status500InternalServerError);
            }
        }
    }
}
