namespace Dosaic.Plugins.Validations.Abstractions;

public interface IValueValidator
{
    string ErrorMessage { get; }
    string Code { get; }
    object Arguments { get; }
    bool TreatNullAsValid { get; }
    Task<bool> IsValidAsync(ValidationContext context, CancellationToken cancellationToken = default);
}
