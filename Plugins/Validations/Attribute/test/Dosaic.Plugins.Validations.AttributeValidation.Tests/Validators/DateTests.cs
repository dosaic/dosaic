using Chronos.Abstractions;
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class DateTests
{
    [Test]
    public async Task CanValidateBefore()
    {
        var validator = new AttributeValidation.Validators.Validations.Date.BeforeAttribute(2020, 3, 3);
        validator.Code.Should().Be(ValidationCodes.Date.Before);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "date" && x.Value!.Equals(new DateTime(2020, 3, 3)));
        var context = new ValidationContext(new DateTime(2020, 3, 2), "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new DateTime(2020, 3, 4) };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateAfter()
    {
        var validator = new AttributeValidation.Validators.Validations.Date.AfterAttribute(2020, 3, 3);
        validator.Code.Should().Be(ValidationCodes.Date.After);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "date" && x.Value!.Equals(new DateTime(2020, 3, 3)));
        var context = new ValidationContext(new DateTime(2020, 3, 4), "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new DateTime(2020, 3, 2) };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    private static IServiceProvider DateServiceProvider(DateTime dt)
    {
        var sc = new ServiceCollection();
        var provider = Substitute.For<IDateTimeProvider>();
        provider.UtcNow.Returns(dt);
        sc.AddSingleton(provider);
        return sc.BuildServiceProvider();
    }

    [Test]
    public async Task CanValidateAge()
    {
        var validator = new AttributeValidation.Validators.Validations.Date.AgeAttribute(17, 18);
        validator.Code.Should().Be(ValidationCodes.Date.Age);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minAge" && x.Value!.Equals(17));
        validator.GetArguments().Should().Contain(x => x.Key == "maxAge" && x.Value!.Equals(18));
        var context = new ValidationContext(new DateTime(2002, 1, 1), "", DateServiceProvider(new DateTime(2020, 1, 1)));
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new DateTime(2000, 1, 2) };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = "test" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMinAge()
    {
        var validator = new AttributeValidation.Validators.Validations.Date.MinAgeAttribute(18);
        validator.Code.Should().Be(ValidationCodes.Date.MinAge);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "age" && x.Value!.Equals(18));
        var context = new ValidationContext(new DateTime(2002, 1, 1), "", DateServiceProvider(new DateTime(2020, 1, 1)));
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new DateTime(2002, 1, 2) };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMaxAge()
    {
        var validator = new AttributeValidation.Validators.Validations.Date.MaxAgeAttribute(18);
        validator.Code.Should().Be(ValidationCodes.Date.MaxAge);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "age" && x.Value!.Equals(18));
        var context = new ValidationContext(new DateTime(2002, 1, 1), "", DateServiceProvider(new DateTime(2020, 1, 1)));
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = new DateTime(2001, 1, 1) };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
