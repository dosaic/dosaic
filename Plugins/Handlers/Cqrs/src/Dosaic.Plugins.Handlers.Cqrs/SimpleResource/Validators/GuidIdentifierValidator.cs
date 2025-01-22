using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators
{
    public static class GuidIdentifierValidator
    {
        public static void Validate(AbstractValidator<IGuidIdentifier> validator)
        {
            validator.RuleFor(v => v.Id).NotEmpty();
        }
    }
}
