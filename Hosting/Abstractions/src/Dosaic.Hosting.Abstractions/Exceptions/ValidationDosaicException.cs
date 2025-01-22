using Dosaic.Hosting.Abstractions.Middlewares.Models;
using Microsoft.AspNetCore.Http;

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
