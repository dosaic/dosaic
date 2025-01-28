using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class BoolTests
{
    [Test]
    public async Task CanTestForTrue()
    {
        var validator = new AttributeValidation.Validators.Validations.Bool.TrueAttribute();
        validator.Code.Should().Be(ValidationCodes.Bool.True);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(true, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = false };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanTestForFalse()
    {
        var validator = new AttributeValidation.Validators.Validations.Bool.FalseAttribute();
        validator.Code.Should().Be(ValidationCodes.Bool.False);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(false, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = true };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
