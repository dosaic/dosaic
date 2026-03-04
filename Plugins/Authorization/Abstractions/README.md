# Dosaic.Plugins.Authorization.Abstractions

`Dosaic.Plugins.Authorization.Abstractions` provides shared authorization primitives for the Dosaic plugin ecosystem. It defines a model for role-based authorization policies (`AuthPolicy`) and a set of extension helpers (`PolicyExtensions`) that make it straightforward to register pre-built or custom policies with ASP.NET Core's authorization middleware.

Concrete authentication plugins (e.g. `Dosaic.Plugins.Authorization.Keycloak`) depend on this package to declare and wire up their policies in a uniform way.

## Installation

```shell
dotnet add package Dosaic.Plugins.Authorization.Abstractions
```

Or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Authorization.Abstractions" Version="" />
```

## Types

### `AuthPolicy`

Represents a named ASP.NET Core authorization policy that requires the authenticated user to hold one or more roles.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | The policy name used when calling `[Authorize(Policy = "...")]`. |
| `Roles` | `IList<string>` | The roles the user must possess for the policy to succeed. |

---

### `PolicyExtensions`

A static helper class that exposes two pre-built policies and two extension methods for `AuthorizationOptions`.

#### Pre-built policies

| Field | Type | Description |
|---|---|---|
| `AuthenticatedPolicy` | `AuthorizationPolicy` | Requires the user to be authenticated (`RequireAuthenticatedUser()`). |
| `AllowAllPolicy` | `AuthorizationPolicy` | Allows every request regardless of authentication state (`RequireAssertion(_ => true)`). |

#### Extension methods

| Method | Description |
|---|---|
| `AddDefaultPolicy(AuthorizationOptions, AuthorizationPolicy)` | Sets the supplied policy as both the default policy and registers it under the name `"DEFAULT"`. |
| `AddAuthPolicies(AuthorizationOptions, IEnumerable<AuthPolicy>)` | Iterates a collection of `AuthPolicy` objects and registers each one as a named role-based policy. |

## Usage

### Registering the default "authenticated" policy

```csharp
using Dosaic.Plugins.Authorization.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

services.AddAuthorization(options =>
{
    // Every endpoint that does not specify an explicit policy
    // will require an authenticated user.
    options.AddDefaultPolicy(PolicyExtensions.AuthenticatedPolicy);
});
```

### Registering a permissive (allow-all) policy — useful for development or disabled auth

```csharp
services.AddAuthorization(options =>
{
    options.AddDefaultPolicy(PolicyExtensions.AllowAllPolicy);
});
```

### Registering role-based policies from configuration

```csharp
var policies = new List<AuthPolicy>
{
    new AuthPolicy { Name = "AdminOnly", Roles = ["admin"] },
    new AuthPolicy { Name = "ReadWrite", Roles = ["editor", "admin"] },
};

services.AddAuthorization(options =>
{
    options.AddDefaultPolicy(PolicyExtensions.AuthenticatedPolicy);
    options.AddAuthPolicies(policies);
});
```

Apply the named policy on a controller or endpoint:

```csharp
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    // Only users with the "admin" role can reach these endpoints.
}
```

### Driving policies from `appsettings`

When using the Keycloak plugin, `AuthPolicy` instances are typically loaded from configuration:

```yaml
keycloak:
  enabled: true
  host: keycloak.example.com
  clientId: my-service
  policies:
    - name: AdminOnly
      roles:
        - admin
    - name: ReadWrite
      roles:
        - editor
        - admin
```

The Keycloak plugin reads the `policies` list as `IList<AuthPolicy>` and passes it to `options.AddAuthPolicies(...)` during `ConfigureServices`.
