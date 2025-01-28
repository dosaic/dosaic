using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    public class Bool
    {
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class TrueAttribute : SyncValidationAttribute
        {
            public override string ErrorMessage => "Value must be true";
            public override string Code => ValidationCodes.Bool.True;
            public override object Arguments => new { };
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is true;
            }
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
        public class FalseAttribute : SyncValidationAttribute
        {
            public override string ErrorMessage => "Value must be false";
            public override string Code => ValidationCodes.Bool.False;
            public override object Arguments => new { };
            protected override bool IsValid(ValidationContext context)
            {
                return context.Value is false;
            }
        }
    }
}
