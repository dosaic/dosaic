using AwesomeAssertions;
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class ArrayTests
{
    [Test]
    public async Task CanTestLength()
    {
        var validator = new AttributeValidation.Validators.Validations.Array.LengthAttribute(1, 2);
        validator.Code.Should().Be(ValidationCodes.Array.Length);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minimum" && x.Value!.Equals(1));
        validator.GetArguments().Should().Contain(x => x.Key == "maximum" && x.Value!.Equals(2));
        var context = new ValidationContext(new[] { "123" }, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = Array.Empty<string>() };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = new[] { "123", "456", "789" } };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = "" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanTestMinLength()
    {
        var validator = new AttributeValidation.Validators.Validations.Array.MinLengthAttribute(1);
        validator.Code.Should().Be(ValidationCodes.Array.MinLength);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minimum" && x.Value!.Equals(1));
        var context = new ValidationContext(new[] { "123" }, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new[] { "123", "456", "789" } };
        result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = Array.Empty<string>() };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanTestMaxLength()
    {
        var validator = new AttributeValidation.Validators.Validations.Array.MaxLengthAttribute(1);
        validator.Code.Should().Be(ValidationCodes.Array.MaxLength);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "maximum" && x.Value!.Equals(1));
        var context = new ValidationContext(new[] { "123" }, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = Array.Empty<string>() };
        result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new[] { "123", "456", "789" } };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
