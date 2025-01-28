using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    public class Int
    {
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class RangeAttribute(int minimum, int maximum) : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.Int.Range;
            public override object Arguments => new { minimum, maximum };
            public override string ErrorMessage => $"Number must be at least {minimum} and at most {maximum}";
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is int number && number >= minimum && number <= maximum;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MaxAttribute(int maximum) : RangeAttribute(int.MinValue, maximum)
        {
            private readonly int _maximum = maximum;
            public override string Code => ValidationCodes.Int.Max;
            public override object Arguments => new { maximum = _maximum };
            public override string ErrorMessage => $"Number must be at most {_maximum}";
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MinAttribute(int minimum) : RangeAttribute(minimum, int.MaxValue)
        {
            private readonly int _minimum = minimum;
            public override string Code => ValidationCodes.Int.Min;
            public override object Arguments => new { minimum = _minimum };
            public override string ErrorMessage => $"Number must be at least {_minimum}";
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class PositiveAttribute : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.Int.Positive;
            public override object Arguments => new { };
            public override string ErrorMessage => "Number must be positive";
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is > 0;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class NegativeAttribute : SyncValidationAttribute
        {
            public override string Code => ValidationCodes.Int.Negative;
            public override object Arguments => new { };
            public override string ErrorMessage => "Number must be positive";
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is < 0;
            }
        }
    }
}
