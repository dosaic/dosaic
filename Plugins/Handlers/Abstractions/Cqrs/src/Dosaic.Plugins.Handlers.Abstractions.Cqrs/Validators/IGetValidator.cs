using FluentValidation;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators
{
    public interface IGetValidator<TResource> : IBaseValidator
    {
        void ValidateOnGet(AbstractValidator<TResource> validator);
    }
}
