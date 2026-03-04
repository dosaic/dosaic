# Dosaic.Plugins.Authorization.Keycloak

`Dosaic.Plugins.Authorization.Keycloak` is a plugin that provides JWT-based authentication and role-driven authorization against a [Keycloak](https://www.keycloak.org/) server. It implements a custom ASP.NET Core authentication scheme that validates Bearer tokens using Keycloak's realm public key, maps realm and client roles to `ClaimTypes.Role`, and exposes named authorization policies that can be applied to minimal API endpoints or MVC controllers.

## Features

- **Custom `keycloak` authentication scheme** — validates JWT Bearer tokens without a full OIDC discovery round-trip; fetches the realm public key once and caches it in memory
- **Realm & resource-access role mapping** — automatically extracts roles from the Keycloak `realm_access` and `resource_access` JWT claims and adds them as `ClaimTypes.Role` claims
- **Named authorization policies** — define policies by name with required roles in configuration; policies are registered with ASP.NET Core's `IAuthorizationService` automatically
- **Disable mode** — set `Enabled: false` to run without any authentication (an allow-all default policy is registered), useful for local development
- **Health check integration** — registers a URL health check against the Keycloak management endpoint (defaults to port `9000`, path `/health/ready`) tagged as `readiness`
- **OpenTelemetry instrumentation** — emits `authentication_keycloak_success` and `authentication_keycloak_noresult` counters plus distributed tracing spans per authentication attempt
- **Separate health-check host** — the health check target can differ from the authentication authority (e.g. internal management port vs. public HTTPS endpoint)

## Installation

```shell
dotnet add package Dosaic.Plugins.Authorization.Keycloak
```

Or add the package reference directly to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Authorization.Keycloak" Version="" />
```

## Configuration

The plugin is configured under the `keycloak` key via the `[Configuration("keycloak")]` attribute on `KeycloakPluginConfiguration`.

### Configuration classes

```csharp
[Configuration("keycloak")]
public class KeycloakPluginConfiguration
{
    // Toggle the entire plugin. When false, an allow-all authorization policy is registered
    // and no authentication middleware is added.
    public bool Enabled { get; set; }

    // Hostname of the Keycloak authentication server (required when Enabled = true)
    public string Host { get; set; }

    // Optional port for the authority URI (omit to use the default HTTPS/HTTP port)
    public int? Port { get; set; }

    // When true, uses http:// instead of https:// for the authority URI
    public bool Insecure { get; set; }

    // OAuth2 client ID (used to resolve resource_access roles)
    public string ClientId { get; set; }

    // OAuth2 client secret
    public string ClientSecret { get; set; }

    // Realm URL configuration
    public RealmsConfig Realms { get; set; } = new RealmsConfig();

    // Named authorization policies available in the application
    public IList<AuthPolicy> Policies { get; set; } = new List<AuthPolicy>();

    // Health check endpoint configuration (defaults to port 9000, path /health/ready)
    public HealthCheckConfig HealthCheck { get; set; } = new HealthCheckConfig();
}

public class RealmsConfig
{
    // URL path prefix used to build the realm public-key endpoint
    public string Prefix { get; set; } = "/auth/realms";

    // Default realm name
    public string Default { get; set; } = "master";
}

public class HealthCheckConfig
{
    // When true, uses http:// for the health check URL (default: true)
    public bool Insecure { get; set; } = true;

    // Hostname for the Keycloak management/health endpoint
    public string Host { get; set; }

    // Port for the management endpoint (default: 9000)
    public int? Port { get; set; } = 9000;

    // Path for the health check (default: /health/ready)
    public string Prefix { get; set; } = "/health/ready";
}
```

### `appsettings.yml` example

```yaml
keycloak:
  enabled: true
  host: keycloak.example.com   # public Keycloak hostname
  port: 443                    # optional; omit to use default HTTPS port
  insecure: false              # false = https (recommended for production)
  clientId: my-service
  clientSecret: supersecret
  realms:
    prefix: /auth/realms
    default: master
  policies:
    - name: READ
      roles:
        - API_PERMISSIONS_READ
    - name: WRITE
      roles:
        - API_PERMISSIONS_WRITE
  healthCheck:
    host: keycloak-internal.example.com  # internal management host
    port: 9000
    insecure: true                       # management port typically not TLS
    prefix: /health/ready
```

### Disable for local development

```yaml
keycloak:
  enabled: false
```

When `enabled` is `false`, no authentication is required and all requests are allowed through. A warning is logged at startup.

## Usage

### Minimal API endpoints

```csharp
public class MyEndpoints : IPluginEndpointsConfiguration
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder, IServiceProvider serviceProvider)
    {
        // Require named policy per verb
        endpointRouteBuilder
            .AddSimpleRestResource<MyResource>(serviceProvider, "my-resource")
            .ForGet(cfg => cfg.WithPolicies("READ"))
            .ForGetList(cfg => cfg.WithPolicies("READ"))
            .ForPost(cfg => cfg.WithPolicies("WRITE"))
            .ForPut(cfg => cfg.WithPolicies("WRITE"))
            .ForDelete(cfg => cfg.WithPolicies("WRITE"))
            .ForAll(cfg => cfg.WithPolicies("WRITE", "READ")); // apply to all verbs at once

        // Or use standard ASP.NET Core policy requirement directly
        endpointRouteBuilder.MapGet("/hello", () => "Hello World!").RequireAuthorization("READ");
    }
}
```

### MVC controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    [HttpGet]
    [Authorize("READ")]
    public IActionResult GetAll() => Ok();

    [HttpPost]
    [Authorize("WRITE")]
    public IActionResult Create([FromBody] Item item) => Ok();

    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public() => Ok("no auth needed");
}
```

### How token validation works

1. The `Authorization: Bearer <token>` header is extracted from the incoming request.
2. The JWT is decoded to read the `iss` (issuer) claim, which contains the Keycloak realm URL.
3. `PublicKeyService` fetches the realm's public key from `<authority><realms.prefix>/<realm>` and caches it in memory for the lifetime of the process.
4. The token is validated using `JwtSecurityTokenHandler` with `ValidateIssuerSigningKey = true`, `ValidateLifetime = true`, and `ValidateAudience = false`.
5. Roles are extracted from the `realm_access.roles` array and — when a client ID is present (`azp` claim) — also from `resource_access.<clientId>.roles`.
6. All extracted roles are added as `ClaimTypes.Role` claims, making them available to `[Authorize(Roles = "...")]` and `RequireRole` policies.

### Authorization policies

Policies are registered from `KeycloakPluginConfiguration.Policies` and require the user to have **all** listed roles:

```csharp
// Registered automatically — no manual code required:
// options.AddPolicy("READ",  builder => builder.RequireRole("API_PERMISSIONS_READ"));
// options.AddPolicy("WRITE", builder => builder.RequireRole("API_PERMISSIONS_WRITE"));
```

The **default policy** requires an authenticated user (i.e., a valid Keycloak token). Unauthenticated requests to unprotected endpoints are still allowed.

### Health check

The plugin registers a URL health check named `keycloak` tagged as `readiness`:

```
GET http(s)://<healthCheck.host>:<healthCheck.port><healthCheck.prefix>
```

With the defaults above this resolves to `http://keycloak-internal.example.com:9000/health/ready`. The health check returns `Unhealthy` if the endpoint does not respond with a success status code.
