namespace Dosaic.Plugins.Validations.Abstractions
{
    public class ValidationError
    {
        public required string Validator { get; init; }
        public required IDictionary<string, object> Arguments { get; init; }
        public required string Path { get; init; }
        public required string Code { get; init; }
        public required string Message { get; init; }
    }
}
