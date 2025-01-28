# Dosaic.Plugins.Validations.AttributeValidation

Dosaic.Plugins.Validations.AttributeValidation is a `plugin` that allows other `Dosaic components` to `validate object using attributes`.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Validations.AttributeValidation
```
or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Plugins.Validations.AttributeValidation" Version="" />
```

## Appsettings.yml

You do not need to configure anything, because the implementation resolver, does this automatically at startup.

## Usage

The validator is auto-registered using the Plugin technology. You can use it by adding the `Validations` attribute to your model and inject the IValidator in your usage.

However, there are following Validations attibutes:

- Required
- Expression
- Array
    - Length
    - MinLength
    - MaxLength
- Bool
    - True
    - False
- Date
    - Before
    - After
    - Age
    - MinAge
    - MaxAge
- Int
    - Range
    - Min
    - Max
    - Positive
    - Negative
- String
    - Length
    - MinLength
    - MaxLength
    - Email
    - Url
    - Regex
```csharp

Example:
```csharp
internal class DbModel {
    [Validations.Required]
    public string Id {get;set;}
    [Validations.String.MinLength(5), Validations.String.MaxLength(10), Validations.String.Regex(@"^[a-zA-Z]+$")]
    public string LongName {get;set;}
    [Validations.Expression("Value % 2 == 0")]
    public int SomeNumber {get;set;}
}

// USAGE:
IServiceProvider sp;
var validator = sp.GetRequiredService<IValidator>();
var dbModel = new DbModel { Id = "1", LongName = "LongName", SomeNumber = 2 };
var result = await validator.ValidateAsync(dbModel);
Console.WriteLine(result.IsValid); // true
var failModel = new DbModel { Id = "", LongName = "..", SomeNumber = 3 };
var result = await validator.ValidateAsync(dbModel);
Console.WriteLine(result.IsValid); // false
Console.WriteLine(result.Errors); // List of errors
```


