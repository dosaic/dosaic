using FluentValidation;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators
{
    public class GenericValidator<T> : AbstractValidator<T>
    {
        public GenericValidator(Action<AbstractValidator<T>> validator) => validator(this);
    }
}
