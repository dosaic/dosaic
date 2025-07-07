using AwesomeAssertions;
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests.Validators;

public class StringTests
{
    [Test]
    public async Task CanValidateLength()
    {
        var validator = new AttributeValidation.Validators.Validations.String.LengthAttribute(1, 2);
        validator.Code.Should().Be(ValidationCodes.String.Length);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "minimum" && x.Value!.Equals(1));
        validator.GetArguments().Should().Contain(x => x.Key == "maximum" && x.Value!.Equals(2));
        var context = new ValidationContext("t", "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = "123" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMinLength()
    {
        var validator = new AttributeValidation.Validators.Validations.String.MinLengthAttribute(1);
        validator.Code.Should().Be(ValidationCodes.String.MinLength);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "length" && x.Value!.Equals(1));
        var context = new ValidationContext("t", "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = "" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    public async Task CanValidateMaxLength()
    {
        var validator = new AttributeValidation.Validators.Validations.String.MaxLengthAttribute(1);
        validator.Code.Should().Be(ValidationCodes.String.MaxLength);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "length" && x.Value!.Equals(1));
        var context = new ValidationContext("t", "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = "ta" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
        context = context with { Value = null };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }

    [Test]
    [TestCase("test@test.de", true)]
    [TestCase("test-user@test.de", true)]
    [TestCase("testtest.de", false)]
    [TestCase("testtest@te", false)]
    [TestCase("", false)]
    public async Task CanValidateEmail(string email, bool shouldBeValid)
    {
        var validator = new AttributeValidation.Validators.Validations.String.EmailAttribute();
        validator.Code.Should().Be(ValidationCodes.String.Email);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(email, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().Be(shouldBeValid);
    }

    [Test]
    [TestCase("http://test.eu", true)]
    [TestCase("https://test.eu", true)]
    [TestCase("ftp://test.eu", true)]
    [TestCase("ssh://test.eu", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public async Task CanValidateUrls(string url, bool shouldBeValid)
    {
        var validator = new AttributeValidation.Validators.Validations.String.UrlAttribute();
        validator.Code.Should().Be(ValidationCodes.String.Url);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().BeEmpty();
        var context = new ValidationContext(url, "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().Be(shouldBeValid);
    }

    [Test]
    public async Task CanValidateRegex()
    {
        const string Pattern = "^[a-zA-Z0-9]*$";
        var validator = new AttributeValidation.Validators.Validations.String.RegexAttribute(Pattern);
        validator.Code.Should().Be(ValidationCodes.String.Regex);
        validator.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        validator.GetArguments().Should().Contain(x => x.Key == "pattern" && x.Value!.Equals(Pattern));
        var context = new ValidationContext("test", "", TestingDefaults.ServiceProvider());
        var result = await validator.IsValidAsync(context);
        result.Should().BeTrue();
        context = context with { Value = "!asd" };
        result = await validator.IsValidAsync(context);
        result.Should().BeFalse();
    }
}
