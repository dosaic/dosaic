using System.Collections;
using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    public class Array
    {
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class LengthAttribute(int minimum, int maximum) : SyncValidationAttribute
        {
            public override string ErrorMessage => $"Array length must be between {minimum} and {maximum}";
            public override string Code => ValidationCodes.Array.Length;
            public override object Arguments => new { minimum, maximum };
            protected override bool IsValid(ValidationContext context)
            {
                if (context.Value is not IEnumerable enumerable || context.Value is string)
                    return false;
                var count = enumerable.OfType<object>().Count();
                return count >= minimum && count <= maximum;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MinLengthAttribute(int minimum) : LengthAttribute(minimum, int.MaxValue)
        {
            private readonly int _minimum = minimum;
            public override string ErrorMessage => $"Array length must be at least {_minimum}";
            public override string Code => ValidationCodes.Array.MinLength;
            public override object Arguments => new { minimum = _minimum };
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MaxLengthAttribute(int maximum) : LengthAttribute(0, maximum)
        {
            private readonly int _maximum = maximum;
            public override string ErrorMessage => $"Array length must be at most {_maximum}";
            public override string Code => ValidationCodes.Array.MaxLength;
            public override object Arguments => new { maximum = _maximum };
        }
    }
}
