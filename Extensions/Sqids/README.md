# Dosaic.Extensions.Sqids

Dosaic.Extensions.Sqids is an extension library that provides string methods to encode and decode values using the [Sqids](https://sqids.org/) algorithm — producing short, YouTube-like identifiers that are URL-safe and reversible.

## Installation

```shell
dotnet add package Dosaic.Extensions.Sqids
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Extensions.Sqids" Version="" />
```

## Features

- **`ToSqid()`** — encode any string (including Unicode) into a Sqid
- **`FromSqid()`** — decode a Sqid back to the original string (guaranteed round-trip)
- **Default encoder** — pre-configured with a shuffled alphabet and minimum length of 10
- **Custom encoder** — pass a `SqidsEncoder<char>` instance to override encoding options per-call
- **Global encoder override** — replace the default encoder application-wide via `SqidExtensions.Encoder`
- **Null safety** — both methods throw `ArgumentNullException` immediately for `null` inputs
- **Unicode support** — encodes and decodes non-ASCII strings (e.g. Chinese, emoji) correctly

## Usage

### Basic Encoding and Decoding

```csharp
using Dosaic.Extensions.Sqids;

// Encode a string to a Sqid
string sqid = "HelloWorld".ToSqid();
// e.g. "kKs7PVdXUYnH" (length >= 10, URL-safe)

// Decode back to the original string
string original = sqid.FromSqid();
// "HelloWorld"
```

Round-tripping is guaranteed with the same encoder:

```csharp
string value = "ComplexString123";
string result = value.ToSqid().FromSqid();
// result == "ComplexString123"
```

### Unicode and Special Characters

```csharp
string unicode = "你好世界".ToSqid();
string decoded = unicode.FromSqid();
// decoded == "你好世界"

string special = "!@#$%^&*()".ToSqid();
```

### Custom Encoder

Supply a `SqidsEncoder<char>` directly to override options on a per-call basis without affecting the global encoder:

```csharp
using Dosaic.Extensions.Sqids;
using Sqids;

var encoder = new SqidsEncoder<char>(new SqidsOptions
{
    Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789",
    MinLength = 5
});

string sqid     = "Hello".ToSqid(encoder);
string original = sqid.FromSqid(encoder);
// original == "Hello"
```

## Configuration

The default encoder is a static property and can be replaced application-wide:

```csharp
using Dosaic.Extensions.Sqids;
using Sqids;

SqidExtensions.Encoder = new SqidsEncoder<char>(new SqidsOptions
{
    Alphabet  = "yourCustomAlphabetHere",
    MinLength = 12
});
```

**Default settings:**

| Option | Value |
|---|---|
| `Alphabet` | `kKsW7PVdXUYnHgQ6rujl0GepfNzB2qZ9bC83IyDmOAtJ4hcSvM1Roaw5LxEiTF` |
| `MinLength` | `10` |

## Use Cases

- Creating short, URL-friendly identifiers from arbitrary strings
- Obfuscating sequential or predictable IDs in public-facing URLs
- Generating consistent, reversible short codes for sharing links
