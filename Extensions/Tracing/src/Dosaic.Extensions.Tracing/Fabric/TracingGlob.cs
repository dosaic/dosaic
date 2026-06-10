using System.Text.RegularExpressions;
using Metalama.Framework.Aspects;

namespace Dosaic.Extensions.Tracing
{
    /// <summary>
    ///     Namespace/type-name glob matching for the include/exclude filters.
    ///     <c>*</c> matches a single name segment (no dots); <c>**</c> matches across segments.
    /// </summary>
    [RunTimeOrCompileTime]
    internal static class TracingGlob
    {
        public static bool Matches(string fullName, string[] includes, string[] excludes)
        {
            if (excludes != null)
                foreach (var exclude in excludes)
                    if (IsMatch(fullName, exclude))
                        return false;

            if (includes is null or { Length: 0 })
                return true;

            foreach (var include in includes)
                if (IsMatch(fullName, include))
                    return true;

            return false;
        }

        public static bool IsMatch(string input, string pattern)
        {
            // Convert glob to regex: ** → .*  and  * → [^.]*
            var regex = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^.]*") + "$";
            return Regex.IsMatch(input, regex);
        }
    }
}
