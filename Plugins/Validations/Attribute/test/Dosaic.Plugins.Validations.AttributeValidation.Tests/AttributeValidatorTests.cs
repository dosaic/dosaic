using AwesomeAssertions;
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Testing.NUnit;
using NUnit.Framework;

namespace Dosaic.Plugins.Validations.AttributeValidation.Tests;

public class ValidationsTests
{
    private IValidator _validator;

    [SetUp]
    public void Setup()
    {
        _validator = new AttributeValidator(TestingDefaults.ServiceProvider());
    }

    private static ComplexDto GetComplexDto(Action<ComplexDto> configure = null)
    {
        var dto = new ComplexDto
        {
            Firstname = "John",
            Lastname = "Abruzzi",
            Age = 25,
            Birthdate = new DateTime(1980, 1, 1),
            IsActive = true,
            Children = new List<SomeDto>
            {
                new() { Field = "12345" },
                new() { Field = "12345" },
            },
            SingleDto = new SomeDto { Field = "12345" },
            NestedChildren = []
        };
        configure?.Invoke(dto);
        return dto;
    }

    [Test]
    public async Task CanValidateValidModels()
    {
        var dto = new SomeDto { Field = "12345" };
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().HaveCount(0);
    }

    [Test]
    public async Task CanValidateInvalidModels()
    {
        var dto = new SomeDto { Field = "1234" };
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.String.MinLength);
        result.Errors[0].Path.Should().Be("Field");
    }

    [Test]
    public async Task CanValidateComplexModels()
    {
        var dto = GetComplexDto();
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().HaveCount(0);
    }

    [Test]
    public async Task CanValidateListProperties()
    {
        var dto = GetComplexDto(x => x.Children = new List<SomeDto>());
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.Array.MinLength);
        result.Errors[0].Path.Should().Be("Children");

        dto = GetComplexDto(x => x.Children = new List<SomeDto> { new() { Field = "1234" } });
        result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.String.MinLength);
        result.Errors[0].Path.Should().Be("Children/0/Field");

        dto = GetComplexDto(x => x.NestedChildren = [[new SomeDto { Field = "123" }]]);
        result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.String.MinLength);
        result.Errors[0].Path.Should().Be("NestedChildren/0/0/Field");
    }

    [Test]
    public async Task CanValidateObjectProperties()
    {
        var dto = GetComplexDto(x => x.SingleDto.Field = "1");
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.String.MinLength);
        result.Errors[0].Path.Should().Be("SingleDto/Field");
    }

    [Test]
    public async Task CanValidatePrimitiveProperties()
    {
        var dto = GetComplexDto(x =>
        {
            x.Firstname = "1";
            x.Lastname = "B";
            x.Age = 17;
            x.Birthdate = new DateTime(1969, 1, 1);
            x.IsActive = false;
        });
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Path == "Firstname" && x.Code == ValidationCodes.String.MinLength);
        result.Errors.Should().Contain(x => x.Path == "Lastname" && x.Code == ValidationCodes.String.Regex);
        result.Errors.Should().Contain(x => x.Path == "Lastname" && x.Code == ValidationCodes.Expression);
        result.Errors.Should().Contain(x => x.Path == "Age" && x.Code == ValidationCodes.Int.Range);
        result.Errors.Should().Contain(x => x.Path == "Birthdate" && x.Code == ValidationCodes.Date.After);
        result.Errors.Should().Contain(x => x.Path == "IsActive" && x.Code == ValidationCodes.Bool.True);

        dto = GetComplexDto(x =>
        {
            x.Firstname = "123456";
            x.Lastname = null!;
            x.Birthdate = new DateTime(3000, 1, 1);
            x.Children = null!;
            x.SingleDto = null!;
        });
        result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Path == "Firstname" && x.Code == ValidationCodes.String.MaxLength);
        result.Errors.Should().Contain(x => x.Path == "Lastname" && x.Code == ValidationCodes.Required);
        result.Errors.Should().Contain(x => x.Path == "Birthdate" && x.Code == ValidationCodes.Date.Before);
        result.Errors.Should().Contain(x => x.Path == "Children" && x.Code == ValidationCodes.Required);
        result.Errors.Should().Contain(x => x.Path == "SingleDto" && x.Code == ValidationCodes.Required);
    }

    [Test]
    public async Task CanValidateNull()
    {
        var result = await _validator.ValidateAsync(null);
        result.IsValid.Should().BeTrue();
        result = await _validator.ValidateAsync(null, [new AttributeValidation.Validators.Validations.RequiredAttribute()]);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Path == "" && x.Code == ValidationCodes.Required);
    }

    [Test]
    public async Task ValidationErrorHasImportantData()
    {
        var dto = GetComplexDto(x => x.Firstname = "1");
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        var error = result.Errors[0];
        error.Path.Should().Be("Firstname");
        error.Code.Should().Be(ValidationCodes.String.MinLength);
        error.Message.Should().Be("String must be at least 2 characters long");
        error.Validator.Should().Be("MinLength");
        error.Arguments.Should().ContainKey("length").WhoseValue.Should().Be(2);
    }

    [Test]
    public async Task CanHandleExceptionsInValidation()
    {
        var throwDto = new ThrowDto { Field = "123" };
        var result = await _validator.ValidateAsync(throwDto);
        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationCodes.GenericError);
        result.Errors[0].Path.Should().Be("Field");
    }

    [Test]
    public async Task NullablesWillBeNotAssertedWhenNull()
    {
        var dto = new RequiredDto();
        var result = await _validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.Required);
        result.Errors[0].Path.Should().Be(nameof(RequiredDto.Required));

        var dto2 = new RequiredDto { Required = "123" };
        result = await _validator.ValidateAsync(dto2);
        result.IsValid.Should().BeTrue();

        var dto3 = new RequiredDto { Required = "123", Nullable = "1" };
        result = await _validator.ValidateAsync(dto3);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be(ValidationCodes.String.MinLength);
        result.Errors[0].Path.Should().Be(nameof(RequiredDto.Nullable));
    }
}

public class SomeDto
{
    [AttributeValidation.Validators.Validations.String.MinLength(5)]
    public required string Field { get; set; }
}

[AttributeValidation.Validators.Validations.Required]
public class ComplexDto
{
    [AttributeValidation.Validators.Validations.String.MinLength(2), AttributeValidation.Validators.Validations.String.MaxLength(5)]
    public required string Firstname { get; set; }

    [AttributeValidation.Validators.Validations.Required, AttributeValidation.Validators.Validations.Expression("Value.StartsWith('A')"), AttributeValidation.Validators.Validations.String.Regex("^[aA].*")]
    public required string Lastname { get; set; }

    [AttributeValidation.Validators.Validations.Int.Range(18, 30)]
    public required int Age { get; set; }

    [AttributeValidation.Validators.Validations.Date.After(1970), AttributeValidation.Validators.Validations.Date.Before(2000)]
    public required DateTime Birthdate { get; set; }

    [AttributeValidation.Validators.Validations.Bool.True]
    public bool IsActive { get; set; }

    [AttributeValidation.Validators.Validations.Required, AttributeValidation.Validators.Validations.Array.MinLength(1), AttributeValidation.Validators.Validations.Array.MaxLength(3)]
    public required IList<SomeDto> Children { get; set; }

    [AttributeValidation.Validators.Validations.Required]
    public required SomeDto SingleDto { get; set; }

    [AttributeValidation.Validators.Validations.Required]
    public required IList<IList<SomeDto>> NestedChildren { get; set; }
}

public class ThrowDto
{
    [Throw]
    public required string Field { get; set; }
}

public class ThrowAttribute : SyncValidationAttribute
{
    public override string ErrorMessage => "Always throws";
    public override string Code => "Throw";
    public override object Arguments => new { };
    protected override bool IsValid(ValidationContext context) => throw new Exception();
}

public class RequiredDto
{
    [AttributeValidation.Validators.Validations.Required]
    public string Required { get; set; }

    [AttributeValidation.Validators.Validations.String.MinLength(2)]
    public string Nullable { get; set; }
}
