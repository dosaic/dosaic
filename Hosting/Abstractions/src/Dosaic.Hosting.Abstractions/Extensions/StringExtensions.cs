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

        public static string ToUrlEncoded(this string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            return Uri.EscapeDataString(input);
        }

        public static string FromUrlEncoded(this string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            return Uri.UnescapeDataString(input);
        }
    }
}
