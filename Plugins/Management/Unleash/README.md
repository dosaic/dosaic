# Dosaic.Plugins.Management.Unleash

`Dosaic.Plugins.Management.Unleash` is a Dosaic plugin that integrates [Unleash](https://github.com/Unleash/unleash) feature flag management into ASP.NET Core applications. It bridges the Unleash client SDK with the `Microsoft.FeatureManagement` abstraction, enabling gradual rollouts, experimentation, and kill-switch controls without redeployment.

## Installation

```shell
dotnet add package Dosaic.Plugins.Management.Unleash
```

Or as a `<PackageReference>` in your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Management.Unleash" Version="" />
```

**Dependencies:**
- [`Microsoft.FeatureManagement.AspNetCore`](https://github.com/microsoft/FeatureManagement-Dotnet) — feature flag abstraction
- [`Unleash.Client`](https://github.com/Unleash/unleash-client-dotnet) — Unleash .NET client SDK
- [`AspNetCore.HealthChecks.Uris`](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) — URL-based health check

## Configuration

The plugin reads its settings from the `unleash` section of your application configuration, bound via `[Configuration("unleash")]`.

```yaml
unleash:
  appName: "my-app"
  apiUri: "http://localhost:4242/api/"
  apiToken: "default:development.your-api-token-here"
  instanceTag: "instance-1"
```

| Property | Description |
|---|---|
| `appName` | Name of your application, reported to the Unleash server |
| `apiUri` | Full URI to your Unleash API, e.g. `http://localhost:4242/api/` |
| `apiToken` | API token created in the Unleash web UI |
| `instanceTag` | Unique tag to identify this running instance |

## Usage

### Checking a feature flag in code

Inject `IFeatureManager` and call `IsEnabledAsync` with the toggle name as defined in Unleash:

```csharp
using Microsoft.FeatureManagement;

public class OrderService(IFeatureManager featureManager)
{
    public async Task<IActionResult> PlaceOrder(OrderRequest request)
    {
        if (await featureManager.IsEnabledAsync("new-checkout-flow"))
        {
            return await HandleNewCheckout(request);
        }

        return await HandleLegacyCheckout(request);
    }
}
```

### Guarding a controller or action with `[FeatureGate]`

Apply `[FeatureGate]` at the controller or action level. When the toggle is disabled, the plugin returns a `404 Not Found` response (via `FeatureNotEnabledDisabledHandler`).

```csharp
using Microsoft.FeatureManagement.Mvc;

[ApiController, Route("beta")]
[FeatureGate("beta-api")]
public class BetaController : ControllerBase
{
    [HttpGet("feature-a")]
    [FeatureGate("feature-a")]
    public IActionResult GetFeatureA() => Ok("Feature A is active");

    [HttpPost("feature-b")]
    [FeatureGate("feature-b")]
    public IActionResult PostFeatureB() => Ok("Feature B is active");
}
```

### MVC Razor views

Add the tag helper and use the `<feature>` tag to conditionally render view content:

```cshtml
@addTagHelper *, Microsoft.FeatureManagement.AspNetCore

<feature name="dark-mode">
    <link rel="stylesheet" href="~/css/dark.css" />
</feature>

<feature name="dark-mode" negate="true">
    <link rel="stylesheet" href="~/css/light.css" />
</feature>
```

### Conditional middleware

Gate an entire middleware on a feature toggle:

```csharp
app.UseMiddlewareForFeature<AnalyticsMiddleware>("analytics-tracking");
```

### Conditional MVC filters

Register an MVC action filter that is only active when a toggle is enabled:

```csharp
services.AddMvc(options =>
{
    options.Filters.AddForFeature<AuditFilter>("audit-logging");
});
```

### Toggle types

`FeatureToggleType` provides constants for the standard Unleash toggle strategies:

```csharp
using Dosaic.Plugins.Management.Unleash;

// Reference toggle types by their string constant
if (toggle.Type == FeatureToggleType.KillSwitch)
{
    // handle kill-switch logic
}
```

| Constant | Value |
|---|---|
| `FeatureToggleType.Release` | `"release"` |
| `FeatureToggleType.Experiment` | `"experiment"` |
| `FeatureToggleType.Operational` | `"operational"` |
| `FeatureToggleType.KillSwitch` | `"killSwitch"` |
| `FeatureToggleType.Permission` | `"permission"` |

## Features

- **Automatic Unleash context propagation** — `UnleashMiddlware` (`[Middleware(50)]`) builds an `UnleashContext` per request, populating `UserId` (from `HttpContext.User.Identity.Name`), `AppName`, `CurrentTime`, `RemoteAddress`, and (optionally) `SessionId` when session middleware is registered.
- **`Microsoft.FeatureManagement` integration** — `UnleashFeatureDefinitionProvider` exposes all Unleash toggles as `FeatureDefinition` instances, and `UnleashFilter` (`[FilterAlias("Unleash")]`) evaluates them via the Unleash client, making `IFeatureManager` the single API for all feature checks.
- **Disabled feature handler** — when a `[FeatureGate]`-protected endpoint is accessed but the flag is off, `FeatureNotEnabledDisabledHandler` raises a `NotFoundDosaicException`, resulting in a `404` response.
- **Health check** — registers a readiness URL health check against `{apiUri}/health` under the name `unleash`.
- **OpenTelemetry metrics** — emits four counters automatically:

  | Metric | Description |
  |---|---|
  | `dosaic_unleash_plugin_impressions_total` | Number of impression events (labelled by `featureName`, `enabled`) |
  | `dosaic_unleash_plugin_errors_total` | Number of Unleash client error events |
  | `dosaic_unleash_plugin_toggleUpdates_total` | Number of toggle cache refresh events |
  | `dosaic_unleash_plugin_unleash_filter_calls_total` | Number of `UnleashFilter` evaluations (labelled by `featureName`, `isEnabled`) |

- **Structured logging** — impression events are logged at `Debug`, toggle updates at `Information`, and errors at `Error` level.
