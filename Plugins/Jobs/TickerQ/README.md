# Dosaic.Plugins.Jobs.TickerQ

Dosaic.Plugins.Jobs.TickerQ is a plugin that allows Dosaic-based services to schedule and manage background jobs using [TickerQ](https://github.com/nickofc/TickerQ). It supports recurring (cron) jobs, fire-and-forget jobs, delayed jobs, PostgreSQL or in-memory storage, Redis caching, a built-in dashboard, OpenTelemetry tracing, and Prometheus metrics.

## Installation

```shell
dotnet add package Dosaic.Plugins.Jobs.TickerQ
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Jobs.TickerQ" Version="" />
```

## Configuration

Configure `appsettings.yml` (or `appsettings.json`) with the `tickerq` section:

```yaml
tickerq:
  # PostgreSQL storage (ignored when inMemory: true)
  host: localhost
  port: 5432
  database: postgres
  user: postgres
  password: postgres

  # Use in-memory storage instead of PostgreSQL (useful for development)
  inMemory: true

  # Redis caching for job state (optional)
  useRedis: false
  redisConnectionString: localhost:6379

  # Schema name for database tables
  schema: ticker

  # Dashboard settings
  dashboardBasePath: /tickerq/dashboard
  dashboardAuthMode: Host     # None, Basic, ApiKey, Host
  allowedDashboardHost: localhost
  # dashboardUsername: admin   # for Basic auth
  # dashboardPassword: secret  # for Basic auth
  # dashboardApiKey: my-key    # for ApiKey auth

  # Enable Microsoft Feature Management integration to toggle jobs via config
  enableJobsByFeatureManagementConfig: false

  # Optional tuning
  pollingIntervalInMs: 5000
  maxConcurrency: 10
  schedulerTimeZone: UTC
```

### Configuration class reference

```csharp
[Configuration("tickerq")]
public class TickerQConfiguration
{
    public string Host { get; set; }
    public int Port { get; set; } = 5432;
    public string Database { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
    public bool InMemory { get; set; }
    public bool UseRedis { get; set; }
    public string RedisConnectionString { get; set; }
    public string Schema { get; set; } = "ticker";
    public string AllowedDashboardHost { get; set; }
    public string DashboardBasePath { get; set; } = "/tickerq/dashboard";
    public DashboardAuthMode DashboardAuthMode { get; set; } = DashboardAuthMode.Host;
    public string DashboardUsername { get; set; }
    public string DashboardPassword { get; set; }
    public string DashboardApiKey { get; set; }
    public bool EnableJobsByFeatureManagementConfig { get; set; }
    public int? MaxConcurrency { get; set; }
    public int? PollingIntervalInMs { get; set; }
    public string SchedulerTimeZone { get; set; }

    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
}
```

### Dashboard authentication modes

| Mode | Description |
|---|---|
| `None` | No authentication — dashboard is open to all |
| `Basic` | HTTP Basic authentication using `dashboardUsername` / `dashboardPassword` |
| `ApiKey` | API key header authentication using `dashboardApiKey` |
| `Host` | Restrict access to requests from `allowedDashboardHost` |

## Usage

### Defining jobs

#### Simple async job (no parameters)

Implement `IAsyncJob` directly:

```csharp
public class SendDailyReportJob : IAsyncJob
{
    private readonly IReportService _reportService;

    public SendDailyReportJob(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = default)
    {
        await _reportService.SendAsync(cancellationToken);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
```

#### Parameterized async job

Implement `IParameterizedAsyncJob<T>` when the job requires input:

```csharp
public class ProcessOrderJob : IParameterizedAsyncJob<int>
{
    private readonly IOrderService _orderService;
    private readonly ILogger<ProcessOrderJob> _logger;

    public ProcessOrderJob(IOrderService orderService, ILogger<ProcessOrderJob> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task ExecuteAsync(TickerFunctionContext<int> context,
        CancellationToken cancellationToken = default)
    {
        var orderId = context.Request;
        await _orderService.ProcessAsync(orderId, cancellationToken);
        _logger.LogInformation("Processed order {OrderId}", orderId);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
```

### Registering recurring jobs

#### Option 1 — attribute-based auto-registration

Annotate the job class with `[RecurringJob]`. The plugin discovers and registers it automatically at startup via the TickerQ seeder:

```csharp
[RecurringJob("0 0 * * *")]              // every day at midnight
public class SendDailyReportJob : IAsyncJob { ... }

[RecurringJob("0 * * * *")]              // every hour
public class HourlyCalculationJob : IAsyncJob { ... }
```

The `[RecurringJob]` attribute accepts a standard cron expression.

#### Option 2 — programmatic registration via `ConfigureJobs`

Register jobs in your plugin or host configuration using the `IJobManager` API:

```csharp
public class MyPlugin : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.ConfigureJobs(async (jobs, _) =>
        {
            await jobs.RegisterRecurringAsync<SendDailyReportJob>("0 0 * * *");
        });
    }
}
```

### Fire-and-forget and delayed jobs via `IJobManager`

Inject `IJobManager` to enqueue or schedule jobs programmatically at runtime:

```csharp
public class OrderController
{
    private readonly IJobManager _jobs;

    public OrderController(IJobManager jobs) => _jobs = jobs;

    public async Task PlaceOrder(int orderId)
    {
        // fire-and-forget
        await _jobs.EnqueueAsync<ProcessOrderJob, int>(orderId);

        // simple job (no parameters)
        await _jobs.EnqueueAsync<SendDailyReportJob>();

        // delayed execution by TimeSpan
        await _jobs.ScheduleAsync<SendDailyReportJob>(TimeSpan.FromHours(1));
        await _jobs.ScheduleAsync<ProcessOrderJob, int>(orderId, TimeSpan.FromMinutes(5));

        // delayed execution to a specific DateTime
        await _jobs.ScheduleAsync<SendDailyReportJob>(DateTime.UtcNow.AddHours(2));
    }
}
```

### `IJobManager` interface

All job management operations are asynchronous and return `Guid` identifiers:

```csharp
public interface IJobManager
{
    Task<Guid> EnqueueAsync<TJob>(CancellationToken cancellationToken = default)
        where TJob : IAsyncJob;

    Task<Guid> EnqueueAsync<TJob, TJobParams>(TJobParams parameters,
        CancellationToken cancellationToken = default)
        where TJob : IParameterizedAsyncJob<TJobParams>;

    Task<Guid> ScheduleAsync<TJob>(TimeSpan delay, CancellationToken cancellationToken = default)
        where TJob : IAsyncJob;

    Task<Guid> ScheduleAsync<TJob>(DateTime executionTime, CancellationToken cancellationToken = default)
        where TJob : IAsyncJob;

    Task<Guid> ScheduleAsync<TJob, TJobParams>(TJobParams parameters, TimeSpan delay,
        CancellationToken cancellationToken = default)
        where TJob : IParameterizedAsyncJob<TJobParams>;

    Task<Guid> ScheduleAsync<TJob, TJobParams>(TJobParams parameters, DateTime executionTime,
        CancellationToken cancellationToken = default)
        where TJob : IParameterizedAsyncJob<TJobParams>;

    Task RegisterRecurringAsync<TJob>(string cronExpression,
        CancellationToken cancellationToken = default)
        where TJob : IAsyncJob;

    Task DeleteRecurringAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

### Dashboard

The TickerQ dashboard is mounted at the path configured in `dashboardBasePath` (default: `/tickerq/dashboard`). Access is controlled via the `dashboardAuthMode` setting.

## Attributes

### `[RecurringJob(cronPattern)]`

Marks a class for automatic recurring job registration at startup. Accepts a cron pattern. This attribute is shared with the Hangfire plugin via `Dosaic.Plugins.Jobs.Abstractions`.

```csharp
[RecurringJob("*/5 * * * *")]
public class PollExternalApiJob : IAsyncJob { ... }
```

### `[JobTimeout(timeout, TimeUnit)]`

Cancels the job after the specified duration. This attribute is shared with the Hangfire plugin via `Dosaic.Plugins.Jobs.Abstractions`.

```csharp
[JobTimeout(30, TimeUnit.Seconds)]
public class QuickJob : IAsyncJob { ... }
```

Supported `TimeUnit` values: `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days`.

### `[JobTimeZone(timeZoneId)]`

Specifies the time zone used for cron schedule evaluation (default: UTC). This attribute is shared with the Hangfire plugin via `Dosaic.Plugins.Jobs.Abstractions`.

```csharp
[JobTimeZone("Europe/Berlin")]
public class LocalTimeCronJob : IAsyncJob { ... }
```

### `[JobPriority(priority)]`

Sets the execution priority for a job. TickerQ-specific attribute.

```csharp
using TickerQ.Utilities.Enums;

[JobPriority(TickerTaskPriority.High)]
public class CriticalJob : IAsyncJob { ... }
```

## Persistence

TickerQ supports multiple persistence backends, configured via the `tickerq` section:

| Mode | Configuration | Description |
|---|---|---|
| In-memory | `inMemory: true` | Jobs stored in memory; lost on restart. Best for development. |
| PostgreSQL | `inMemory: false`, `useRedis: false` | Entity Framework Core with Npgsql. Durable storage for production. |
| Redis | `useRedis: true` | StackExchange.Redis. Fast distributed state. |
| Custom | Via `ITickerQConfigurator` | Plug in any persistence backend. |

## Custom configuration (`ITickerQConfigurator`)

Implement `ITickerQConfigurator` (a `IPluginConfigurator`) to customize the TickerQ options:

```csharp
public class MyTickerQConfigurator : ITickerQConfigurator
{
    // Set to true if your Configure() call registers a persistence backend,
    // so the plugin skips its default storage setup.
    public bool IncludesPersistence => false;

    public void Configure(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options)
    {
        // Custom configuration
    }
}
```

All `ITickerQConfigurator` implementations are discovered automatically by the Dosaic plugin system.

## Observability

### Health check

A TickerQ readiness health check is registered automatically and is accessible via the standard Dosaic health endpoints (`/health/readiness`).

### OpenTelemetry tracing

TickerQ jobs are automatically instrumented with OpenTelemetry tracing via `TickerQ.Instrumentation.OpenTelemetry`.

### Prometheus metrics

A background service (`TickerQStatisticsMetricsReporter`) collects TickerQ statistics every 60 seconds and publishes them as OpenTelemetry gauges:

| Metric | Description |
|---|---|
| `tickerq_job_count_Idle` | Number of idle jobs |
| `tickerq_job_count_Queued` | Number of queued jobs |
| `tickerq_job_count_InProgress` | Number of currently processing jobs |
| `tickerq_job_count_Done` | Number of completed jobs |
| `tickerq_job_count_Failed` | Number of failed jobs |
| `tickerq_job_count_Cancelled` | Number of cancelled jobs |
| `tickerq_job_count_Skipped` | Number of skipped jobs |

## MassTransit Integration

For scheduled messaging with MassTransit, see the companion package `Dosaic.Plugins.Messaging.MassTransit.TickerQ`, which provides an `IMessageScheduler` implementation backed by TickerQ instead of Hangfire.

## Comparison with Hangfire

Both `Dosaic.Plugins.Jobs.Hangfire` and `Dosaic.Plugins.Jobs.TickerQ` share common job attributes (`RecurringJobAttribute`, `JobTimeoutAttribute`, `JobTimeZoneAttribute`) from `Dosaic.Plugins.Jobs.Abstractions`.

| Feature | Hangfire | TickerQ |
|---|---|---|
| Job API | Synchronous, returns `string` IDs | Asynchronous, returns `Guid` IDs |
| Job interfaces | `AsyncJob` base class | `IAsyncJob` interface |
| Queues | Multiple named queues | Single execution pool |
| Dashboard | Hangfire Dashboard | TickerQ Dashboard |
| Storage | PostgreSQL, in-memory | PostgreSQL, Redis, in-memory |
| Feature flags | Integrated via `EnabledByFeatureFilter` | Via configuration |
| Monitoring API | Rich `IMonitoringApi` (queues, states, filtering) | Minimal |
| Job priority | Not supported | `[JobPriority]` attribute |
| Unique per queue | `[UniquePerQueueAttribute]` | Not supported |

Choose Hangfire for mature queue management and monitoring, or TickerQ for a lightweight async-first scheduler with priority support.
