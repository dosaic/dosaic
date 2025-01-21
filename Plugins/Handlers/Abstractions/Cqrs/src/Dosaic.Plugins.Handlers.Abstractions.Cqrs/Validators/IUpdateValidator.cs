using FluentValidation;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators
{
    public interface IUpdateValidator<TResource> : IBaseValidator
    {
        void ValidateOnUpdate(AbstractValidator<TResource> validator);
    }
}
