using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class RequiredTests
{
    [Test]
    public async Task CanValidateRequired()
    {
        var validator = new AttributeValidation.Validators.Validations.RequiredAttribute();
        validator.Code.Should().Be(ValidationCodes.Required);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(new[] { "123" }, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = "" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
