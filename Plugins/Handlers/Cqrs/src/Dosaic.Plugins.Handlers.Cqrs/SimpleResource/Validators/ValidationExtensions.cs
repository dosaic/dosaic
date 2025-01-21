using FluentValidation;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Hosting.Abstractions.Middlewares.Models;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators
{
    public static class ValidationExtensions
    {
        public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken = default)
        {
            if (request is null)
                throw new ValidationDosaicException("One or more validations have failed.", new List<FieldValidationError> { new("request", "Cannot pass null value") });
            var result = await validator.ValidateAsync(request, cancellationToken);
            if (result.IsValid) return;

            var validationErrors = result.Errors
                .Select(x => new FieldValidationError(x.PropertyName, x.ErrorMessage))
                .ToArray();

            throw new ValidationDosaicException("One or more validations have failed.", validationErrors);
        }
    }
}
