using AwesomeAssertions;
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class IntTests
{
    [Test]
    public async Task CanValidateRange()
    {
        var validator = new AttributeValidation.Validators.Validations.Int.RangeAttribute(1, 2);
        validator.Code.Should().Be(ValidationCodes.Int.Range);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minimum" && x.Value!.Equals(1));
        validator.GetArguments().Should().Contain(x => x.Key == "maximum" && x.Value!.Equals(2));
        var context = new ValidationContext(1, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 3 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMin()
    {
        var validator = new AttributeValidation.Validators.Validations.Int.MinAttribute(1);
        validator.Code.Should().Be(ValidationCodes.Int.Min);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minimum" && x.Value!.Equals(1));
        var context = new ValidationContext(1, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 0 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMax()
    {
        var validator = new AttributeValidation.Validators.Validations.Int.MaxAttribute(1);
        validator.Code.Should().Be(ValidationCodes.Int.Max);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "maximum" && x.Value!.Equals(1));
        var context = new ValidationContext(0, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 2 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidatePositive()
    {
        var validator = new AttributeValidation.Validators.Validations.Int.PositiveAttribute();
        validator.Code.Should().Be(ValidationCodes.Int.Positive);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(1, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 0 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateNegative()
    {
        var validator = new AttributeValidation.Validators.Validations.Int.NegativeAttribute();
        validator.Code.Should().Be(ValidationCodes.Int.Negative);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(-1, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = 0 };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
