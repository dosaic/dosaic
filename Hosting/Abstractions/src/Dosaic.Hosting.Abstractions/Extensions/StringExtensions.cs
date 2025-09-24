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
    }
}
