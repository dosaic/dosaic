# Dosaic.Plugins.Jobs.Abstractions

Shared attributes and types for Dosaic job plugins. This package provides common building blocks used by both `Dosaic.Plugins.Jobs.Hangfire` and `Dosaic.Plugins.Jobs.TickerQ`.

## Shared Attributes

### `[RecurringJob(cronPattern, queue)]`

Marks a job class for automatic recurring registration at startup. Accepts a cron pattern and an optional queue name (default: `"default"`).

```csharp
[RecurringJob("0 0 * * *")]
public class DailyJob : IAsyncJob { ... }
```

### `[JobTimeout(timeout, TimeUnit)]`

Cancels a job after the specified duration.

```csharp
[JobTimeout(30, TimeUnit.Seconds)]
public class QuickJob : IAsyncJob { ... }
```

Supported `TimeUnit` values: `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days`.

### `[JobTimeZone(timeZoneId)]`

Specifies the time zone for cron schedule evaluation.

```csharp
[JobTimeZone("Europe/Berlin")]
public class LocalTimeCronJob : IAsyncJob { ... }
```
