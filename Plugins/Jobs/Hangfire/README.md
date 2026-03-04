# Dosaic.Plugins.Jobs.Hangfire

Dosaic.Plugins.Jobs.Hangfire is a plugin that allows Dosaic-based services to schedule and manage background jobs using [Hangfire](https://www.hangfire.io/). It supports recurring (cron) jobs, fire-and-forget jobs, delayed jobs, PostgreSQL or in-memory storage, a built-in dashboard, OpenTelemetry tracing, Prometheus metrics, and feature-flag-based job execution control.

## Installation

```shell
dotnet add package Dosaic.Plugins.Jobs.Hangfire
```

or add as a package reference to your `.csproj`:

```xml
<PackageReference Include="Dosaic.Plugins.Jobs.Hangfire" Version="" />
```

## Configuration

Configure `appsettings.yml` (or `appsettings.json`) with the `hangfire` section:

```yaml
hangfire:
  # PostgreSQL storage (ignored when inMemory: true)
  host: localhost
  port: 5432
  database: postgres
  user: postgres
  password: postgres

  # Use in-memory storage instead of PostgreSQL (useful for development)
  inMemory: true

  # Hostname from which the Hangfire dashboard is accessible.
  # Leave empty to disable dashboard access entirely.
  allowedDashboardHost: localhost

  # Enable Microsoft Feature Management integration to toggle jobs via config
  enableJobsByFeatureManagementConfig: false

  # Additional queues to listen on (the "default" queue is always included)
  queues:
    - default
    - critical

  # Optional tuning
  pollingIntervalInMs: 5000       # defaults to Hangfire built-in value
  workerCount: 10                 # defaults to Hangfire built-in value
  invisibilityTimeoutInMinutes: 30
  maxJobArgumentsSizeToRenderInBytes: 4096  # max bytes of job args displayed in dashboard
```

### Configuration class reference

```csharp
[Configuration("hangfire")]
public class HangfireConfiguration
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Database { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
    public bool InMemory { get; set; }
    public string AllowedDashboardHost { get; set; }
    public bool EnableJobsByFeatureManagementConfig { get; set; }
    public int? PollingIntervalInMs { get; set; }
    public int? WorkerCount { get; set; }
    public string[] Queues { get; set; } = [EnqueuedState.DefaultQueue];
    public int InvisibilityTimeoutInMinutes { get; set; } = 30;
    public int MaxJobArgumentsSizeToRenderInBytes { get; set; } = 4096;
    public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
}
```

## Usage

### Defining jobs

#### Simple async job (no parameters)

Extend `AsyncJob` and implement `ExecuteJobAsync`:

```csharp
public class SendDailyReportJob : AsyncJob
{
    private readonly IReportService _reportService;

    public SendDailyReportJob(ILogger<SendDailyReportJob> logger, IReportService reportService)
        : base(logger)
    {
        _reportService = reportService;
    }

    protected override async Task<object> ExecuteJobAsync(CancellationToken cancellationToken)
    {
        await _reportService.SendAsync(cancellationToken);
        return "done";
    }
}
```

#### Parameterized async job

Extend `ParameterizedAsyncJob<T>` when the job requires input:

```csharp
public class ProcessOrderJob : ParameterizedAsyncJob<int>
{
    private readonly IOrderService _orderService;

    public ProcessOrderJob(ILogger<ProcessOrderJob> logger, IOrderService orderService)
        : base(logger)
    {
        _orderService = orderService;
    }

    protected override async Task<object> ExecuteJobAsync(int orderId, CancellationToken cancellationToken)
    {
        var result = await _orderService.ProcessAsync(orderId, cancellationToken);
        Logger.LogInformation("Processed order {OrderId}", orderId);
        return result;
    }
}
```

> **Note:** Job input parameters and results are serialized to JSON and displayed in the Hangfire dashboard. Avoid passing sensitive data as job parameters.

### Registering recurring jobs

#### Option 1 — attribute-based auto-registration

Annotate the job class with `[RecurringJob]`. The plugin discovers and registers it automatically at startup — no boilerplate required:

```csharp
[RecurringJob("0 0 * * *")]              // every day at midnight UTC
public class SendDailyReportJob : AsyncJob { ... }

[RecurringJob("0 * * * *", "critical")]  // every hour, on the "critical" queue
public class CriticalHourlyJob : AsyncJob { ... }
```

The `[RecurringJob]` attribute accepts a standard cron expression and an optional queue name.

#### Option 2 — programmatic registration via `ConfigureJobs`

Register jobs in your plugin or host configuration using the `IJobManager` API:

```csharp
public class MyPlugin : IPluginServiceConfiguration
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.ConfigureJobs((jobs, _) =>
        {
            // simple recurring job
            jobs.RegisterRecurring<SendDailyReportJob>("0 0 * * *");

            // parameterized recurring job (passes the value at registration time)
            jobs.RegisterRecurring<ProcessOrderJob, int>(42, Cron.Daily());

            // recurring job on a specific queue with a name suffix (useful for multiple instances)
            jobs.RegisterRecurring<CriticalHourlyJob>("0 * * * *", queue: "critical", jobSuffix: "v2");
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

    public void PlaceOrder(int orderId)
    {
        // fire-and-forget
        _jobs.Enqueue<ProcessOrderJob, int>(orderId);

        // fire-and-forget on a specific queue
        _jobs.Enqueue<ProcessOrderJob, int>(orderId, queue: "critical");

        // simple job (no parameters)
        _jobs.Enqueue<SendDailyReportJob>();

        // delayed execution
        _jobs.Schedule<SendDailyReportJob>(TimeSpan.FromHours(1));
        _jobs.Schedule<ProcessOrderJob, int>(orderId, TimeSpan.FromMinutes(5));
    }
}
```

### Querying job state via `IJobManager`

`IJobManager` exposes monitoring APIs to inspect the current state of the job store:

```csharp
// all recurring jobs
IList<RecurringJobDto> recurring = jobManager.GetRecurringJobs();

// recurring jobs for a specific type, with optional predicate
IList<RecurringJobDto> myJobs = jobManager.GetRecurringJobs<ProcessOrderJob>();

// enqueued, processing, failed, fetched — all support type-filtered overloads
IList<EnqueuedJobDto>   enqueued   = jobManager.GetEnqueuedJobs<ProcessOrderJob>();
IList<ProcessingJobDto> processing = jobManager.GetProcessingJobs();
IList<FailedJobDto>     failed     = jobManager.GetFailedJobs();

// unified view across all states with optional predicate
IList<JobEntity> all = jobManager.GetJobs(e => e.Type == JobType.Failed);

// delete a recurring or background job
jobManager.DeleteRecurring("ProcessOrder");
jobManager.Delete(backgroundJobId);
```

### Dashboard

The Hangfire dashboard is mounted at `/hangfire`. Access is restricted to the host configured in `allowedDashboardHost`. If the value is empty or not set, access is denied for all hosts.

## Attributes

### `[RecurringJob(cronPattern, queue)]`

Marks a class for automatic recurring job registration at startup. Accepts a cron pattern and an optional queue name.

```csharp
[RecurringJob("*/5 * * * *", "default")]
public class PollExternalApiJob : AsyncJob { ... }
```

### `[JobTimeout(timeout, TimeUnit)]`

Cancels the job after the specified duration. The cancellation token passed to `ExecuteJobAsync` is cancelled automatically.

```csharp
[JobTimeout(30, TimeUnit.Seconds)]
public class QuickJob : AsyncJob { ... }
```

Supported `TimeUnit` values: `Milliseconds`, `Seconds`, `Minutes`, `Hours`, `Days`.

### `[JobTimeZone(TimeZoneInfo)]`

Specifies the time zone used for cron schedule evaluation of recurring jobs (default: UTC).

```csharp
[JobTimeZone(/* TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin") */)]
public class LocalTimeCronJob : AsyncJob { ... }
```

### `[UniquePerQueueAttribute(queue)]`

Prevents duplicate job executions. If a job with the same type and arguments is already queued, the new one is deleted instead of enqueued.

```csharp
[UniquePerQueueAttribute("default")]
public class ImportDataJob : AsyncJob { ... }
```

Optional properties:
- `CheckScheduledJobs` — also check scheduled (delayed) jobs (default: `false`)
- `CheckRunningJobs` — also check currently processing jobs (default: `false`)

### `[JobCleanupExpirationTimeAttribute(days)]`

Controls how many days job results are retained in the storage backend before deletion.

```csharp
[JobCleanupExpirationTimeAttribute(14)]
public class ArchiveJob : AsyncJob { ... }
```

## Filters

### `LogJobExecutionFilter` (always active)

Automatically logs a structured message at the start and finish of every job execution using the job's own logger type.

### `EnabledByFeatureFilter` (opt-in)

Gates job execution on a feature flag using the [Microsoft Feature Management](https://github.com/microsoft/FeatureManagement-Dotnet) system. Enable via configuration:

```yaml
hangfire:
  enableJobsByFeatureManagementConfig: true

featureManagement:
  SendDailyReportJob: true   # job class name is the feature flag name
  ProcessOrderJob: false     # this job will be skipped
```

Works with both file-based feature management and the [Dosaic Unleash plugin](https://dosaic.gitbook.io/dosaic/plugins/management/unleash) for dynamic runtime feature flags. Since the flag is resolved before each job execution, changes take effect at runtime with a delay based on how frequently the feature management source is refreshed.

## Custom storage / server configuration (`IHangfireConfigurator`)

Implement `IHangfireConfigurator` (a `IPluginConfigurator`) to plug in a custom Hangfire storage backend or to configure the background server options:

```csharp
public class MyHangfireConfigurator : IHangfireConfigurator
{
    // Set to true if your Configure() call registers a storage backend,
    // so the plugin skips its default PostgreSQL storage setup.
    public bool IncludesStorage => true;

    public void Configure(IGlobalConfiguration config)
    {
        config.UseRedisStorage("localhost:6379");
    }

    public void ConfigureServer(BackgroundJobServerOptions options)
    {
        options.WorkerCount = 5;
    }
}
```

All `IHangfireConfigurator` implementations are discovered automatically by the Dosaic plugin system.

## Observability

### Health check

A Hangfire readiness health check is registered automatically and is accessible via the standard Dosaic health endpoints (`/health/readiness`). It verifies that at least one Hangfire server is running.

### OpenTelemetry tracing

Hangfire jobs are automatically instrumented with OpenTelemetry tracing via `OpenTelemetry.Instrumentation.Hangfire`.

### Prometheus metrics

A background service (`HangfireStatisticsMetricsReporter`) collects Hangfire statistics every 60 seconds and publishes them as OpenTelemetry gauges:

| Metric | Description |
|---|---|
| `hangfire_job_count_Succeeded` | Number of succeeded jobs |
| `hangfire_job_count_Failed` | Number of failed jobs |
| `hangfire_job_count_Scheduled` | Number of scheduled (delayed) jobs |
| `hangfire_job_count_Processing` | Number of currently processing jobs |
| `hangfire_job_count_Enqueued` | Number of enqueued jobs |
| `hangfire_job_count_Deleted` | Number of deleted jobs |
| `hangfire_job_count_Recurring` | Number of registered recurring jobs |
| `hangfire_job_count_Servers` | Number of active Hangfire servers |
| `hangfire_job_count_Queues` | Number of active queues |
| `hangfire_job_count_RetryJobs` | Number of jobs currently awaiting retry |

## Job naming convention

The job ID used by Hangfire is derived from the class name by stripping the `Job` and `Async` suffixes. For example:

- `SendDailyReportJob` → `SendDailyReport`
- `ProcessOrderAsyncJob` → `ProcessOrder`

When registering the same job type multiple times with `jobSuffix`, the suffix is appended: `ProcessOrder_v2`.

## Further reading

- [Official Hangfire documentation](https://docs.hangfire.io/en/latest/)
- [Hangfire best practices](https://docs.hangfire.io/en/latest/best-practices.html)
