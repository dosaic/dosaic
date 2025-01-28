using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation;

public abstract class AsyncValidationAttribute : Attribute, IValueValidator
{
    public abstract string ErrorMessage { get; }
    public abstract string Code { get; }
    public abstract object Arguments { get; }
    public abstract Task<bool> IsValidAsync(ValidationContext context, CancellationToken cancellationToken = default);
}

public abstract class SyncValidationAttribute : AsyncValidationAttribute
{
    protected abstract bool IsValid(ValidationContext context);
    public override Task<bool> IsValidAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsValid(context));
    }
}
