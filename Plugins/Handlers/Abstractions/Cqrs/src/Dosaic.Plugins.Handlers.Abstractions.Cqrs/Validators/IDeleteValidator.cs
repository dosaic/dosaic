using FluentValidation;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators
{
    public interface IDeleteValidator<TResource> : IBaseValidator
    {
        void ValidateOnDelete(AbstractValidator<TResource> validator);
    }
}
