# Dosaic.Plugins.Validations.Abstractions

Core contracts and shared types for the Dosaic validation subsystem. This package defines the interfaces that all validation plugin implementations must fulfil, along with the result model and a rich set of predefined validation-error codes.

## Installation

```shell
dotnet add package Dosaic.Plugins.Validations.Abstractions
```

## Types

### `IValidator`

The top-level validation entry point. Accepts a model or an explicit list of `IValueValidator` instances and returns a `ValidationResult`.

```csharp
public interface IValidator
{
    // Validate a model using its own declared validators
    Task<ValidationResult> ValidateAsync(object model, CancellationToken cancellationToken = default);

    // Strongly-typed convenience overload
    Task<ValidationResult> ValidateAsync<T>(T model, CancellationToken cancellationToken = default);

    // Validate a single value against an explicit validator list
    Task<ValidationResult> ValidateAsync(object value, IList<IValueValidator> validators, CancellationToken cancellationToken = default);
}
```

---

### `IValueValidator`

Represents a single validation rule applied to one value. Implementations carry their own error message, a structured error code, optional arguments, and a null-handling policy.

```csharp
public interface IValueValidator
{
    string ErrorMessage { get; }
    string Code { get; }
    object Arguments { get; }
    bool TreatNullAsValid { get; }
    Task<bool> IsValidAsync(ValidationContext context, CancellationToken cancellationToken = default);
}
```

| Member | Description |
|---|---|
| `ErrorMessage` | Human-readable description of the constraint violation |
| `Code` | Machine-readable error code (see `ValidationCodes`) |
| `Arguments` | Arbitrary object whose public properties are surfaced in `ValidationError.Arguments` |
| `TreatNullAsValid` | When `true` (default), a `null` value skips this validator |
| `IsValidAsync` | Returns `true` when the value in `context` satisfies the rule |

---

### `ValidationContext`

Immutable record passed into every `IValueValidator.IsValidAsync` call.

```csharp
public sealed record ValidationContext(object Value, string Path, IServiceProvider Services);
```

| Member | Description |
|---|---|
| `Value` | The value being validated |
| `Path` | Dot-notation property path, e.g. `"Address.PostalCode"` |
| `Services` | The application `IServiceProvider` for service-dependent validators |
| `ValueType` | `Value.GetType()` — `null`-safe |
| `IsNullValue` | `true` when `Value` is `null` |
| `IsObjectType` | `true` for plain class instances (not string, not collection, not dictionary) |
| `IsListType` | `true` for any `IEnumerable` that is not a string |

---

### `ValidationResult`

Returned by every `IValidator.ValidateAsync` call.

```csharp
public class ValidationResult
{
    public required ValidationError[] Errors { get; init; }
    public bool IsValid => Errors.Length == 0;
}
```

---

### `ValidationError`

Describes a single constraint violation.

```csharp
public class ValidationError
{
    public required string Validator { get; init; }
    public required IDictionary<string, object> Arguments { get; init; }
    public required string Path { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
}
```

| Property | Description |
|---|---|
| `Validator` | Name of the `IValueValidator` that produced this error |
| `Arguments` | Key/value pairs from the validator's `Arguments` object |
| `Path` | Property path where the violation occurred |
| `Code` | Structured error code from `ValidationCodes` |
| `Message` | Human-readable error message from the validator |

---

### `ValidationCodes`

A static hierarchy of well-known, string-typed error-code constants used by all built-in validators.

| Category | Constant | Code value |
|---|---|---|
| *(root)* | `GenericError` | `"InternalError"` |
| *(root)* | `Required` | `"Required"` |
| *(root)* | `Expression` | `"Expression"` |
| *(root)* | `Unique` | `"Unique"` |
| *(root)* | `InvalidType` | `"InvalidType"` |
| `String` | `MinLength` | `"String.MinLength"` |
| `String` | `MaxLength` | `"String.MaxLength"` |
| `String` | `Length` | `"String.Length"` |
| `String` | `Regex` | `"String.Regex"` |
| `String` | `Email` | `"String.Email"` |
| `String` | `Url` | `"String.Url"` |
| `Int` | `Range` | `"Number.Range"` |
| `Int` | `Min` | `"Number.Min"` |
| `Int` | `Max` | `"Number.Max"` |
| `Int` | `Negative` | `"Number.Negative"` |
| `Int` | `Positive` | `"Number.Positive"` |
| `Date` | `Before` | `"Date.Before"` |
| `Date` | `After` | `"Date.After"` |
| `Date` | `Age` | `"Date.Age"` |
| `Date` | `MinAge` | `"Date.MinAge"` |
| `Date` | `MaxAge` | `"Date.MaxAge"` |
| `Bool` | `True` | `"Bool.True"` |
| `Bool` | `False` | `"Bool.False"` |
| `Array` | `Length` | `"Array.Length"` |
| `Array` | `MinLength` | `"Array.MinLength"` |
| `Array` | `MaxLength` | `"Array.MaxLength"` |

---

### `ValueValidatorExtensions`

Extension methods that simplify working with `IValueValidator` implementations.

| Method | Description |
|---|---|
| `GetArguments(this IValueValidator)` | Reflects over `Arguments` and returns a `Dictionary<string, object>` of its public properties |
| `GetName(this IValueValidator)` | Returns the validator's class name, stripping a trailing `Attribute` suffix when present |

## Usage

### Injecting and using `IValidator`

```csharp
public class CreateUserHandler(IValidator validator)
{
    public async Task HandleAsync(CreateUserRequest request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
                Console.WriteLine($"[{error.Code}] {error.Path}: {error.Message}");

            return;
        }

        // proceed with valid request …
    }
}
```

### Implementing a custom `IValueValidator`

```csharp
using Dosaic.Plugins.Validations.Abstractions;

public class PositiveNumberValidator : IValueValidator
{
    public string ErrorMessage => "Value must be a positive number";
    public string Code => ValidationCodes.Int.Positive;
    public object Arguments => new { };
    public bool TreatNullAsValid => true;

    public Task<bool> IsValidAsync(ValidationContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsNullValue) return Task.FromResult(true);
        if (context.Value is int i) return Task.FromResult(i > 0);
        return Task.FromResult(false);
    }
}
```

### Validating a value against an explicit validator list

```csharp
var validators = new List<IValueValidator> { new PositiveNumberValidator() };
var result = await validator.ValidateAsync(userInput, validators, ct);

if (!result.IsValid)
    Console.WriteLine(result.Errors[0].Message);
```

### Inspecting a validator via extensions

```csharp
IValueValidator v = new PositiveNumberValidator();

var name = v.GetName();                   // "PositiveNumber"
var args = v.GetArguments();              // Dictionary<string, object>
```

