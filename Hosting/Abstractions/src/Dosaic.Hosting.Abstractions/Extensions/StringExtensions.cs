using System.Text.RegularExpressions;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static partial class StringExtensions
    {
        public static string ToSnakeCase(this string input)
        {
            var startUnderscores = RegexUnderscores().Match(input);
            return startUnderscores + RegexNames().Replace(input, "$1_$2").ToLower();
        }

        [GeneratedRegex(@"^_+")]
        private static partial Regex RegexUnderscores();

        [GeneratedRegex(@"([a-z0-9])([A-Z])")]
        private static partial Regex RegexNames();

        public static string ToUrlEncoded(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            return Uri.EscapeDataString(input);
        }

        public static string FromUrlEncoded(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            return Uri.UnescapeDataString(input);
        }


        public static T? ParseEnum<T>(this string? value) where T : struct, Enum
        {
            if (Enum.TryParse<T>(value, true, out var enumValue) && Enum.IsDefined(enumValue))
                return enumValue;
            return null;
        }

        public static bool TryParseEnum(this string? value, Type enumType, out object? enumValue)
        {
            enumValue = null;
            if (!Enum.TryParse(enumType, value, true, out var eValue) || !Enum.IsDefined(enumType, eValue)) return false;
            enumValue = eValue;
            return true;
        }

        public static string? NormalizeNullAndEmptyValues(this string? value)
        {
            return string.IsNullOrEmpty(value)
            || value.Equals("unbekannt", StringComparison.InvariantCultureIgnoreCase)
            || value.Equals("null", StringComparison.InvariantCultureIgnoreCase)
            ? null : value;
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value.Substring(0, maxLength);
        }
    }
}
