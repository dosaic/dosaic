using Chronos.Abstractions;
using Dosaic.Plugins.Validations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    public class Date
    {
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class BeforeAttribute(int year, int month = 1, int day = 1) : SyncValidationAttribute
        {
            private readonly DateTime _date = new(year, month, day);
            public override string ErrorMessage => $"Date must be before {_date:yyyy-MM-dd}";
            public override string Code => ValidationCodes.Date.Before;
            public override object Arguments => new { date = _date };
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is DateTime dt && dt <= _date;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class AfterAttribute(int year, int month = 1, int day = 1) : SyncValidationAttribute
        {
            private readonly DateTime _date = new(year, month, day);
            public override string ErrorMessage => $"Date must be after {_date:yyyy-MM-dd}";
            public override string Code => ValidationCodes.Date.After;
            public override object Arguments => new { date = _date };
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is DateTime dt && dt >= _date;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class AgeAttribute(int minAge, int maxAge) : SyncValidationAttribute
        {
            public override string ErrorMessage => $"Date must be between {minAge} and {maxAge} years";
            public override string Code => ValidationCodes.Date.Age;
            public override object Arguments => new { minAge, maxAge };
            protected override bool IsValid(ValidationContext context)
            {
                if (context.Value is not DateTime dt) return false;
                var now = context.Services.GetRequiredService<IDateTimeProvider>().UtcNow;
                var calculatedAge = now.Year - dt.Year;
                if (dt.Date > now.AddYears(-calculatedAge))
                    calculatedAge--;
                return calculatedAge >= minAge && calculatedAge <= maxAge;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MinAgeAttribute(int age) : AgeAttribute(age, int.MaxValue)
        {
            private readonly int _age = age;
            public override string ErrorMessage => $"Date must be at least {_age} years ago";
            public override string Code => ValidationCodes.Date.MinAge;
            public override object Arguments => new { age = _age };
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class MaxAgeAttribute(int age) : AgeAttribute(int.MinValue, age)
        {
            private readonly int _age = age;
            public override string ErrorMessage => $"Date must be at most {_age} years ago";
            public override string Code => ValidationCodes.Date.MaxAge;
            public override object Arguments => new { age = _age };
        }
    }
}
