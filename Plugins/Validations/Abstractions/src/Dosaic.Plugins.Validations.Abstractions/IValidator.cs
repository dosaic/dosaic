namespace Dosaic.Plugins.Validations.Abstractions;

public interface IValidator
{
    Task<ValidationResult> ValidateAsync(object value, IList<IValueValidator> validators, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateAsync(object model, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateAsync<T>(T model, CancellationToken cancellationToken = default) => ValidateAsync((object)model, cancellationToken);
}
