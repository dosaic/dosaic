using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Parameter)]
    public class RequiredAttribute : SyncValidationAttribute
    {
        public override string Code => ValidationCodes.Required;
        public override object Arguments => new { };
        public override string ErrorMessage => "Field is required";
        protected override bool IsValid(ValidationContext context)
        {
            return context.Value is not null
                   && (context.Value is not string str || !string.IsNullOrWhiteSpace(str));
        }
    }
}
