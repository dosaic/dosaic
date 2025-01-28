using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    public class String
    {
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class LengthAttribute(int minimum, int maximum) : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.String.Length;
            public override object Arguments => new { minimum, maximum };
            public override string ErrorMessage => $"String must be at least {minimum} and at most {maximum} characters long";
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is string str && str.Length <= maximum && str.Length >= minimum;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MinLengthAttribute(int length) : LengthAttribute(length, int.MaxValue)
        {
            private readonly int _length = length;
            public override string Code => ValidationCodes.String.MinLength;
            public override object Arguments => new { length = _length };
            public override string ErrorMessage => $"String must be at least {_length} characters long";
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MaxLengthAttribute(int length) : LengthAttribute(0, length)
        {
            private readonly int _length = length;
            public override string Code => ValidationCodes.String.MaxLength;
            public override object Arguments => new { length = _length };
            public override string ErrorMessage => $"String must be at most {_length} characters long";
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class EmailAttribute : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.String.Email;
            public override object Arguments => new { };
            public override string ErrorMessage => "String must be a valid email address";

            private static string DomainMapper(Match match)
            {
                var idn = new IdnMapping();
                var domainName = idn.GetAscii(match.Groups[2].Value);
                return match.Groups[1].Value + domainName;
            }

            [ExcludeFromCodeCoverage(Justification = "Cannot test the timeout pattern in a good way")]
            protected override bool IsValid(ValidationContext context)
            {
                if (context.Value is not string str || string.IsNullOrWhiteSpace(str)) return false;
                // Implementation:
                // https://learn.microsoft.com/en-us/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format?redirectedfrom=MSDN
                try
                {
                    var email = Regex.Replace(str, "(@)(.+)$", DomainMapper,
                        RegexOptions.None, TimeSpan.FromMilliseconds(200));
                    return Regex.IsMatch(email,
                        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                        RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
                }
                catch
                {
                    return false;
                }
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class UrlAttribute : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.String.Url;
            public override object Arguments => new { };
            public override string ErrorMessage => "String must be a valid url";
            protected override bool IsValid(ValidationContext context)
            {
                try
                {
                    if (context.Value is not string str) return false;
                    _ = new Uri(str);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class RegexAttribute([StringSyntax(StringSyntaxAttribute.Regex)] string pattern) : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.String.Regex;
            public override object Arguments => new { pattern };
            public override string ErrorMessage => $"String must match the regex pattern '{pattern}'";

            [ExcludeFromCodeCoverage(Justification = "Cannot test the timeout pattern in a good way")]
            protected override bool IsValid(ValidationContext context)
            {
                try
                {
                    return context.Value is string str
                           && Regex.IsMatch(str, pattern, RegexOptions.None, TimeSpan.FromSeconds(250));
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
