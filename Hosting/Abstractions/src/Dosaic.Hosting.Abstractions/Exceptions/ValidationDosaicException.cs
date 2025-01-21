using Microsoft.AspNetCore.Http;
using Dosaic.Hosting.Abstractions.Middlewares.Models;

namespace Dosaic.Hosting.Abstractions.Exceptions
{
    public class ValidationDosaicException : DosaicException
    {
        public ValidationDosaicException(string message) : base(message)
        {
            HttpStatus = StatusCodes.Status400BadRequest;
        }
        public ValidationDosaicException(string message, IList<FieldValidationError> validationErrors) : base(message)
        {
            HttpStatus = StatusCodes.Status400BadRequest;
            ValidationErrors = validationErrors;
        }

        public IList<FieldValidationError> ValidationErrors { get; init; } = new List<FieldValidationError>();
    }
}
