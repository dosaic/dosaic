namespace Dosaic.Plugins.Validations.Abstractions
{
    public static class ValueValidatorExtensions
    {
        public static IDictionary<string, object> GetArguments(this IValueValidator valueValidator) =>
            valueValidator.Arguments.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(valueValidator.Arguments));

        public static string GetName(this IValueValidator valueValidator) =>
            valueValidator.GetType().Name.EndsWith("Attribute")
                ? valueValidator.GetType().Name[..^9]
                : valueValidator.GetType().Name;
    }
}
