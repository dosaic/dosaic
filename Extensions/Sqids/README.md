# Dosaic.Extensions.Sqids

Dosaic.Extensions.Sqids is an extension library that provides methods to convert strings to and from Sqid format using the Sqids library.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Extensions.Sqids
```

or add as package reference to your .csproj

```xml
<PackageReference Include="Dosaic.Extensions.Sqids" Version="" />
```

## Usage

The extension provides simple methods to convert strings to and from Sqid format.

### Basic Conversion

Convert a string to a Sqid:

```csharp
using Dosaic.Extensions.Sqids;

string originalString = "HelloWorld";
string sqidString = originalString.ToSqid();
```

Convert a Sqid back to the original string:

```csharp
using Dosaic.Extensions.Sqids;

string sqidString = "kKs7PVdXUYnH"; // Example sqid
string originalString = sqidString.FromSqid();
```

### Custom Encoder

You can also use a custom encoder for special use cases:

```csharp
using Dosaic.Extensions.Sqids;
using Sqids;

var customEncoder = new SqidsEncoder<char>(new SqidsOptions
{
    Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
    MinLength = 8
});

string originalString = "HelloWorld";
string sqidString = originalString.ToSqid(customEncoder);
string decodedString = sqidString.FromSqid(customEncoder);
```

## Default Configuration

By default, the extension uses the following configuration:

- Alphabet: "kKsW7PVdXUYnHgQ6rujl0GepfNzB2qZ9bC83IyDmOAtJ4hcSvM1Roaw5LxEiTF"
- Minimum Length: 10

You can modify the default encoder if needed:

```csharp
using Dosaic.Extensions.Sqids;
using Sqids;

SqidExtensions.Encoder = new SqidsEncoder<char>(new SqidsOptions
{
    Alphabet = "yourCustomAlphabet",
    MinLength = 12
});
```

## Use Cases

Sqids are useful for:
- Creating URL-friendly identifiers
- Obfuscating sequential IDs
- Generating short, unique string identifiers
