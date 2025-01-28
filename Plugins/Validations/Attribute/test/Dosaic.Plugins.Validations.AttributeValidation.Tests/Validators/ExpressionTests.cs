using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class ExpressionTests
{
    [Test]
    public async Task CanValidateExpressions()
    {
        const string Expression = "Value == 123";
        var validator = new AttributeValidation.Validators.Validations.ExpressionAttribute(Expression);
        validator.Code.Should().Be(ValidationCodes.Expression);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "expression" && x.Value!.Equals(Expression));
        var context = new ValidationContext(123, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 456 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanHandleInvalidExpressions()
    {
        const string Expression = "ERROR";
        var validator = new AttributeValidation.Validators.Validations.ExpressionAttribute(Expression);
        var context = new ValidationContext(null, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
