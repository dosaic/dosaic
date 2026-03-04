# Dosaic.Plugins.Validations.AttributeValidation

`Dosaic.Plugins.Validations.AttributeValidation` is a Dosaic plugin that enables attribute-based validation of .NET objects. It registers an `IValidator` implementation that recursively inspects models, validates properties against decorating attributes, and returns structured error results — all without requiring any manual wiring.

## Installation

```shell
dotnet add package Dosaic.Plugins.Validations.AttributeValidation
```

Or as a package reference:

```xml
<PackageReference Include="Dosaic.Plugins.Validations.AttributeValidation" Version="" />
```

## Configuration

No additional configuration is required. `AttributeValidationPlugin` implements `IPluginServiceConfiguration` and is discovered automatically by the Dosaic source generator at startup. It registers `IValidator` as a singleton:

```csharp
// Registered automatically — no manual setup needed
services.AddSingleton<IValidator, AttributeValidator>();
```

## Usage

### Validating a model

Inject `IValidator` wherever you need validation:

```csharp
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Plugins.Validations.AttributeValidation.Validators;

public class OrderService(IValidator validator)
{
    public async Task CreateOrder(OrderRequest request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
                Console.WriteLine($"[{error.Path}] {error.Code}: {error.Message}");
            return;
        }
        // proceed...
    }
}
```

### Annotating models

Decorate properties (or classes) with validation attributes from the `Validations` static class:

```csharp
using Dosaic.Plugins.Validations.AttributeValidation.Validators;

public class CreateUserRequest
{
    [Validations.Required]
    public string Id { get; set; }

    [Validations.Required]
    [Validations.String.MinLength(2)]
    [Validations.String.MaxLength(50)]
    public string Firstname { get; set; }

    [Validations.Required]
    [Validations.String.Regex(@"^[a-zA-Z]+$")]
    public string Lastname { get; set; }

    [Validations.String.Email]
    public string Email { get; set; }

    [Validations.String.Url]
    public string ProfileUrl { get; set; }

    [Validations.Int.Range(18, 120)]
    public int Age { get; set; }

    [Validations.Date.After(1900)]
    [Validations.Date.Before(2010)]
    public DateTime Birthdate { get; set; }

    [Validations.Date.MinAge(18)]
    [Validations.Date.MaxAge(100)]
    public DateTime BirthdateByAge { get; set; }

    [Validations.Bool.True]
    public bool AcceptsTerms { get; set; }

    [Validations.Required]
    [Validations.Array.MinLength(1)]
    [Validations.Array.MaxLength(5)]
    public IList<AddressDto> Addresses { get; set; }
}

public class AddressDto
{
    [Validations.Required]
    [Validations.String.MinLength(2)]
    public string City { get; set; }
}
```

### Checking results

```csharp
var request = new CreateUserRequest
{
    Id           = "u-001",
    Firstname    = "Jane",
    Lastname     = "Doe",
    Email        = "jane@example.com",
    Age          = 30,
    Birthdate    = new DateTime(1994, 6, 15),
    AcceptsTerms = true,
    Addresses    = new List<AddressDto> { new() { City = "Berlin" } }
};

var result = await validator.ValidateAsync(request);
Console.WriteLine(result.IsValid); // true

var invalid = new CreateUserRequest { Id = "", Age = 10, AcceptsTerms = false };
var bad = await validator.ValidateAsync(invalid);
Console.WriteLine(bad.IsValid); // false
foreach (var e in bad.Errors)
    Console.WriteLine($"{e.Path} — {e.Code}: {e.Message}");
// Id           — Required:     Field is required
// Age          — Number.Range: Number must be at least 18 and at most 120
// AcceptsTerms — Bool.True:    Value must be true
```

### Validating a value with an explicit validator list

You can validate a bare value against an ad-hoc list of `IValueValidator` instances:

```csharp
var validators = new IValueValidator[]
{
    new Validations.RequiredAttribute(),
    new Validations.String.MinLengthAttribute(5),
};

var result = await validator.ValidateAsync("Hi", validators);
// result.IsValid == false  (too short)
```

### Expression validation

The `Validations.Expression` attribute evaluates a [DynamicExpresso](https://github.com/dynamicexpresso/DynamicExpresso) expression. The expression receives `this` as an `ExpressionContext` with a `Value` property that holds the current field/property value. The interpreter is case-insensitive and supports late binding.

```csharp
public class InvoiceDto
{
    // Must be an even number
    [Validations.Expression("Value % 2 == 0")]
    public int InvoiceNumber { get; set; }

    // String must start with "INV-"
    [Validations.Expression("Value.StartsWith('INV-')")]
    public string Reference { get; set; }
}
```

Expressions that throw an exception are treated as failing validation.

### Nested and list validation

The validator recursively walks nested objects and collections. Error paths use `/` as a separator:

```csharp
// Path examples:
// "Addresses"        — error on the list property itself (e.g. Array.MinLength)
// "Addresses/0/City" — error on City of the first AddressDto
// "Matrix/0/0/Value" — nested lists follow the same pattern
```

### Custom validators

Extend `ValidationAttribute` (or `AsyncValidationAttribute` for async logic) to create reusable validation attributes:

```csharp
using Dosaic.Plugins.Validations.Abstractions;
using Dosaic.Plugins.Validations.AttributeValidation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class MustBePositiveEvenAttribute : ValidationAttribute
{
    public override string ErrorMessage => "Value must be a positive even number";
    public override string Code => "Custom.PositiveEven";
    public override object Arguments => new { };

    protected override bool IsValid(ValidationContext context)
        => context.Value is int n && n > 0 && n % 2 == 0;
}

// Usage:
public class MyModel
{
    [MustBePositiveEven]
    public int Count { get; set; }
}
```

For async validators override `IsValidAsync` directly:

```csharp
public class UniqueEmailAttribute : AsyncValidationAttribute
{
    public override string ErrorMessage => "Email is already taken";
    public override string Code => "Custom.UniqueEmail";
    public override object Arguments => new { };

    public override async Task<bool> IsValidAsync(ValidationContext context, CancellationToken ct = default)
    {
        if (context.Value is not string email) return true;
        var db = context.Services.GetRequiredService<IUserRepository>();
        return !await db.ExistsAsync(email, ct);
    }
}
```

## Built-in Validators

### `Validations.Required`

| Attribute                  | Arguments | Validation Code | Description                                                                                                                                  |
| -------------------------- | --------- | --------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `[Validations.Required]` | —        | `Required`    | Value must not be `null`; strings must not be empty or whitespace. `TreatNullAsValid` is `false` — a `null` value triggers failure. |

---

### `Validations.Expression`

| Attribute                                       | Arguments      | Validation Code | Description                                                                                                                     |
| ----------------------------------------------- | -------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `[Validations.Expression(string expression)]` | `expression` | `Expression`  | Evaluates a DynamicExpresso expression. Reference the property value via `Value`. Returns `false` if the expression throws. |

---

### `Validations.Array`

Applicable to any `IEnumerable` property (excluding strings).

| Attribute                                                | Arguments                | Validation Code     | Description                                      |
| -------------------------------------------------------- | ------------------------ | ------------------- | ------------------------------------------------ |
| `[Validations.Array.Length(int minimum, int maximum)]` | `minimum`, `maximum` | `Array.Length`    | Element count must be ≥ minimum and ≤ maximum. |
| `[Validations.Array.MinLength(int minimum)]`           | `minimum`              | `Array.MinLength` | Element count must be ≥ minimum.                |
| `[Validations.Array.MaxLength(int maximum)]`           | `maximum`              | `Array.MaxLength` | Element count must be ≤ maximum.                |

---

### `Validations.Bool`

| Attribute                    | Arguments | Validation Code | Description              |
| ---------------------------- | --------- | --------------- | ------------------------ |
| `[Validations.Bool.True]`  | —        | `Bool.True`   | Value must be `true`.  |
| `[Validations.Bool.False]` | —        | `Bool.False`  | Value must be `false`. |

---

### `Validations.Date`

Age-based validators resolve the current UTC time from `IDateTimeProvider` (Chronos).

| Attribute                                                           | Arguments              | Validation Code | Description                                                                |
| ------------------------------------------------------------------- | ---------------------- | --------------- | -------------------------------------------------------------------------- |
| `[Validations.Date.Before(int year, int month = 1, int day = 1)]` | `date`               | `Date.Before` | `DateTime` value must be on or before the specified date.                |
| `[Validations.Date.After(int year, int month = 1, int day = 1)]`  | `date`               | `Date.After`  | `DateTime` value must be on or after the specified date.                 |
| `[Validations.Date.Age(int minAge, int maxAge)]`                  | `minAge`, `maxAge` | `Date.Age`    | Age derived from the `DateTime` must be between minAge and maxAge years. |
| `[Validations.Date.MinAge(int age)]`                              | `age`                | `Date.MinAge` | Date must be at least `age` years ago.                                   |
| `[Validations.Date.MaxAge(int age)]`                              | `age`                | `Date.MaxAge` | Date must be at most `age` years ago.                                    |

---

### `Validations.Int`

Applies to `int` values.

| Attribute                                             | Arguments                | Validation Code     | Description                              |
| ----------------------------------------------------- | ------------------------ | ------------------- | ---------------------------------------- |
| `[Validations.Int.Range(int minimum, int maximum)]` | `minimum`, `maximum` | `Number.Range`    | Value must be ≥ minimum and ≤ maximum. |
| `[Validations.Int.Min(int minimum)]`                | `minimum`              | `Number.Min`      | Value must be ≥ minimum.                |
| `[Validations.Int.Max(int maximum)]`                | `maximum`              | `Number.Max`      | Value must be ≤ maximum.                |
| `[Validations.Int.Positive]`                        | —                       | `Number.Positive` | Value must be > 0.                       |
| `[Validations.Int.Negative]`                        | —                       | `Number.Negative` | Value must be < 0.                       |

---

### `Validations.String`

Applies to `string` values.

| Attribute                                                 | Arguments                | Validation Code      | Description                                                      |
| --------------------------------------------------------- | ------------------------ | -------------------- | ---------------------------------------------------------------- |
| `[Validations.String.Length(int minimum, int maximum)]` | `minimum`, `maximum` | `String.Length`    | String length must be ≥ minimum and ≤ maximum characters.      |
| `[Validations.String.MinLength(int length)]`            | `length`               | `String.MinLength` | String must be at least `length` characters long.              |
| `[Validations.String.MaxLength(int length)]`            | `length`               | `String.MaxLength` | String must be at most `length` characters long.               |
| `[Validations.String.Email]`                            | —                       | `String.Email`     | String must be a valid email address (RFC-compliant, IDN-aware). |
| `[Validations.String.Url]`                              | —                       | `String.Url`       | String must be a valid absolute URI.                             |
| `[Validations.String.Regex(string pattern)]`            | `pattern`              | `String.Regex`     | String must match the specified regular expression.              |

---

## Validation Error Structure

Each `ValidationError` in `ValidationResult.Errors` exposes the following properties:

| Property      | Type                            | Description                                                                                                         |
| ------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `Path`      | `string`                      | `/`-separated path to the failing property (e.g. `"Addresses/0/City"`). Empty string for root-level validation. |
| `Code`      | `string`                      | Machine-readable validation code (see tables above).                                                                |
| `Message`   | `string`                      | Human-readable error message.                                                                                       |
| `Validator` | `string`                      | Short name of the validator attribute (e.g.`"MinLength"`, `"Required"`).                                        |
| `Arguments` | `IDictionary<string, object>` | Attribute constructor arguments as a dictionary (e.g.`{ "length": 5 }`).                                          |

`ValidationResult.IsValid` returns `true` when `Errors` is empty.

## Null Handling

By default all built-in validators set `TreatNullAsValid = true`, meaning a `null` property value is skipped unless `[Validations.Required]` is also present. This lets you distinguish truly optional fields from required ones without cluttering annotations.
