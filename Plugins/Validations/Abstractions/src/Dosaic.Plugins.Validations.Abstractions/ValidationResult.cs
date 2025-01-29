namespace Dosaic.Plugins.Validations.Abstractions
{
    public class ValidationResult
    {
        public required ValidationError[] Errors { get; init; }
        public bool IsValid => Errors.Length == 0;
    }
}
