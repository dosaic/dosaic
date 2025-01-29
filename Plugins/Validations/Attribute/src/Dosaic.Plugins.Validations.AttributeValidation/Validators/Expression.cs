using Dosaic.Plugins.Validations.Abstractions;
using DynamicExpresso;

namespace Dosaic.Plugins.Validations.AttributeValidation.Validators;

public partial class Validations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Class)]
    public class ExpressionAttribute(string expression) : SyncValidationAttribute
    {
        private static readonly Interpreter _interpreter = new(InterpreterOptions.DefaultCaseInsensitive | InterpreterOptions.LateBindObject);
        public override string ErrorMessage => "Expression must evaluate to true";
        public override string Code => ValidationCodes.Expression;
        public override object Arguments => new { expression };
        protected override bool IsValid(ValidationContext context)
        {
            try
            {
                return _interpreter.Eval<bool>(expression, new Parameter("this", typeof(ExpressionContext), new ExpressionContext(context)));
            }
            catch
            {
                return false;
            }
        }

        public sealed class ExpressionContext(ValidationContext context)
        {
            public object Value => context.Value;
        }
    }
}
