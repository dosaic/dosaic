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
  pollingIntervalInMs: 5000       # scheduled-job poller interval, defaults to Hangfire built-in value
  workerCount: 10                 # defaults to Hangfire built-in value
  invisibilityTimeoutInMinutes: 30
  maxJobArgumentsSizeToRenderInBytes: 4096  # max bytes of job args displayed in dashboard
  schemaName: hangfire            # PostgreSQL schema holding the Hangfire tables
  queuePollIntervalInMs: 1000     # how often an idle worker asks the queue for work
  useSlidingInvisibilityTimeout: false
  batchChunkSize: 0               # 0 = write a whole batch in exactly one round trip

  # Per queue tuning - every entry gets its own background job server
  queueConfigurations:
    - name: bulk
      workerCount: 100            # workers dedicated to this queue
      prefetchCount: 50           # jobs fetched per polling round trip
      queuePollIntervalInMs: 200
    - name: critical
      workerCount: 10
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
    public string SchemaName { get; set; } = "hangfire";
    public int QueuePollIntervalInMs { get; set; } = 1000;
    public bool UseSlidingInvisibilityTimeout { get; set; }
    public int BatchChunkSize { get; set; }
    public QueueConfiguration[] QueueConfigurations { get; set; } = [];
    public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
}

public class QueueConfiguration
{
    public string Name { get; set; }
    public int? WorkerCount { get; set; }
    public int PrefetchCount { get; set; } = 1;
    public int? QueuePollIntervalInMs { get; set; }
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

### Batch creating jobs (`IJobManager.CreateBatch`)

Creating jobs one by one costs one database round trip per job, which becomes the bottleneck long
before the workers do. The batch API collects any number of jobs — including continuation chains
between them — and writes **all** of them with a single SQL statement, so a batch of 100 000 jobs is
exactly one round trip.

```csharp
// simplest form: one job per parameter set
var ids = await jobManager.EnqueueBatchAsync<ProcessOrderJob, int>(orderIds, queue: "bulk");
var sameButSync = jobManager.EnqueueBatch<ProcessOrderJob, int>(orderIds, "bulk");

// or scheduled, relative or absolute
await jobManager.ScheduleBatchAsync<ProcessOrderJob, int>(orderIds, TimeSpan.FromMinutes(5), "bulk");
await jobManager.ScheduleBatchAtAsync<ProcessOrderJob, int>(orderIds, tomorrowAtThree, "bulk");
jobManager.ScheduleBatch<ProcessOrderJob, int>(orderIds, TimeSpan.FromMinutes(5), "bulk");
```

For anything more involved, build the batch explicitly. Continuations are declared on the batch item
handle and are resolved inside the same statement, so the antecedent job id never has to travel back
to the application first:

```csharp
var batch = jobManager.CreateBatch();

foreach (var orderId in orderIds)
{
    var import = batch.Enqueue<ImportOrderJob, int>(orderId, "bulk");
    var enrich = import.ContinueWith<EnrichOrderJob, int>(orderId, "bulk");
    enrich.ContinueWith<NotifyOrderJob, int>(orderId, options: JobContinuationOptions.OnAnyFinishedState);
}

batch.Schedule<CleanupJob>(TimeSpan.FromHours(1), "maintenance");

var jobIds = await batch.SaveAsync();   // one round trip
var firstImportId = jobIds[0];
```

`IJobBatch` supports `Enqueue`, `Schedule`, `ScheduleAt` and, on every returned `IJobBatchItem`,
`ContinueWith`. After `SaveAsync()` each item exposes its `Id`.

Enqueueing, scheduling and chaining can be mixed freely in one batch, and all of it still costs one
round trip:

```csharp
var batch = jobManager.CreateBatch();

batch.Enqueue<ImportOrderJob, int>(orderId, "bulk");                       // runs now
batch.ScheduleAt<CleanupJob>(tomorrowAtThree, "maintenance");              // runs at a fixed time

var nightly = batch.Schedule<ReportJob, int>(tenantId, TimeSpan.FromHours(8), "reports");
nightly.ContinueWith<MailReportJob, int>(tenantId, "reports");             // chained off a scheduled job

var root = batch.Enqueue<ImportOrderJob, int>(orderId, "bulk");
root.ContinueWith<EnrichOrderJob, int>(orderId, "bulk");                   // fan-out: two continuations
root.ContinueWith<AuditOrderJob, int>(orderId, "audit",
    JobContinuationOptions.OnAnyFinishedState);                            // on the same antecedent

await batch.SaveAsync();
```

A continuation runs as soon as its antecedent finishes; there is no "continue with a delay". Hangfire
cannot route a delayed continuation to a queue (the queue is not carried on the awaiting job), so if
you need a delayed follow-up, schedule it as its own batch entry or put `[Queue]` on the job type.

Things worth knowing:

- **Chaining is batch local.** `ContinueWith` links two jobs of the same batch. Continuing on a job
  that already exists in the storage still goes through `BackgroundJob.ContinueJobWith`, because it
  has to merge into the antecedent's continuation list under a distributed lock.
- **Client side filters are bypassed.** The bulk write goes straight to the storage, so state
  election filters do not run for batched jobs — they issue their own queries and would defeat the
  single round trip. Server side filters (`EnabledByFeatureFilter`, `LogJobExecutionFilter`,
  `[JobTimeout]`, retries) are unaffected.
- **`[UniquePerQueue]` is the exception.** Its fingerprint claim is folded into the same statement, so
  batched jobs are deduplicated without an extra round trip. A job that loses its claim is never
  written and gets a `null` id back (`IJobBatchItem.IsSuppressed`); continuations of a suppressed job
  are suppressed with it.
- **Non-PostgreSQL storages still work.** With `inMemory: true`, or when an `IHangfireConfigurator`
  brings its own storage, the batch falls back to creating the jobs through the regular Hangfire
  client — same API, without the single round trip guarantee.
- `batchChunkSize` caps how many jobs go into one statement. It defaults to `0` (no cap). Chunking
  never splits a continuation chain.
- **Pickup latency.** The bulk write does not raise Hangfire's in-process "queue changed" signal, so
  idle workers pick batched jobs up on their next poll — `queuePollIntervalInMs` (1000 ms by default,
  instead of Hangfire's 15 seconds).

#### Verifying it against a real database

The batching and prefetching behaviour is covered by Testcontainers integration tests that start a
real PostgreSQL, let Hangfire create its real schema, and assert against it — including that a 1000
job batch produces exactly one statement in PostgreSQL's own query log, that batched jobs are
indistinguishable from jobs created by `BackgroundJobClient`, and that a batched continuation chain
actually executes in order on a running `BackgroundJobServer`.

They are marked `[Explicit]` + `[Category("Integration")]`, so a normal `dotnet test` skips them and
never needs Docker:

```bash
dotnet test Plugins/Jobs/Hangfire/test/Dosaic.Plugins.Jobs.Hangfire.Tests --filter TestCategory=Integration
```

### Tuning queues for high volume (workers and prefetching)

Each entry in `queueConfigurations` gets its own background job server, which makes worker count and
fetch behaviour tunable per queue instead of globally:

```yaml
hangfire:
  workerCount: 20                 # used by the shared server and as fallback
  queues: [default, critical]
  queueConfigurations:
    - name: bulk
      workerCount: 100
      prefetchCount: 50
      queuePollIntervalInMs: 200
```

- **`workerCount`** — workers dedicated to that queue. Queues without an entry stay on the shared
  server and use the global `workerCount`.
- **`prefetchCount`** — jobs pulled out of the queue per round trip. Hangfire's stock PostgreSQL
  queue runs one `UPDATE ... LIMIT 1` per job; with `prefetchCount: 50` a single query hands 50 jobs
  to the workers. This is the main throughput lever when a queue is used as a message bus
  replacement. Values greater than 1 require the built-in PostgreSQL storage.
- **`queuePollIntervalInMs`** — how long an empty queue waits before asking again. The global default
  of 1000 ms replaces Hangfire's 15 second default, which is far too slow for bus-like workloads.

Prefetched jobs are marked as fetched immediately, so they are invisible to other servers for
`invisibilityTimeoutInMinutes`. A worker only asks for a job when it is free, so at most
`prefetchCount - 1` jobs are ever buffered; size the prefetch to what your workers can drain quickly
and enable `useSlidingInvisibilityTimeout` when jobs run longer than the invisibility window.

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

Prevents duplicate job executions. If a job with the same type and arguments is already queued, the new one is deleted instead of enqueued. The attribute also owns the queue of the jobs it guards — whatever queue you pass at enqueue time is overridden by the one on the attribute.

```csharp
[UniquePerQueueAttribute("default")]
public class ImportDataJob : AsyncJob { ... }
```

Optional properties:
- `CheckScheduledJobs` — also block while an equivalent job is only scheduled (default: `false`)
- `CheckRunningJobs` — also block while an equivalent job is being processed (default: `false`)
- `ClaimTimeoutInMinutes` — how long a claim survives without being released, as a safety net for
  processes that died mid-flight (default: `1440`). PostgreSQL only.

#### How the check works

The job's identity — type, method, arguments and queue — is hashed into a fingerprint, and that
fingerprint is claimed in a Hangfire set (`dosaic:unique:<queue>`) that has a unique index on
`(key, value)`. Enqueueing writes the claim; the storage decides who wins. The claim is released
again when the job leaves the checked states (when it starts processing, or when it succeeds, fails
or is deleted if `CheckRunningJobs` is set).

That makes the check cost **one round trip regardless of queue depth** — the previous implementation
paged through every enqueued, scheduled and processing job on every single enqueue, so a batch of
N jobs against a queue of depth M cost O(N·M). It also closes the race the scan had: two clients
enqueueing the same job at the same time could both find nothing and both enqueue.

On PostgreSQL the claim is a single `INSERT ... ON CONFLICT`, and it rides along inside the batch
statement, so `[UniquePerQueue]` now applies to jobs created through the batch API too. On other
storages (in-memory, storages brought by an `IHangfireConfigurator`) the check falls back to a
distributed lock around the set — correct, but unable to take over a claim whose owner died.

#### Differences to the previous queue-scanning implementation

The attribute's own surface is unchanged — same constructor, same `Queue`, `CheckScheduledJobs` and
`CheckRunningJobs`, same `DeletedState` reason — but a few edge cases behave differently:

- **The batch API now honours it.** Previously batched jobs were never deduplicated. Duplicates are
  now suppressed and come back with a `null` id, and the attribute's queue wins over the queue passed
  to `Enqueue`/`Schedule`, exactly as it always did outside the batch.
- **`CheckScheduledJobs` blocks at scheduling time**, not when the scheduled job is enqueued. Under
  the old scan, scheduling the same job five times let all five through, and the first one to reach
  the queue then deleted *itself* because its four siblings were still in the schedule set.
- **A job whose worker died no longer blocks.** With `CheckRunningJobs = false` the claim is released
  when the job starts processing. If the worker then dies and the job is requeued by the invisibility
  timeout, an equivalent job is allowed in; the old scan would have blocked it.
- **On non-PostgreSQL storages a claim outlives its owner.** The fallback cannot detect an expired
  claim, so a process that dies between claiming and releasing blocks that fingerprint until the set
  entry is removed. On PostgreSQL `ClaimTimeoutInMinutes` covers this.
- **During a rolling upgrade** jobs enqueued by the old code hold no claim, so one duplicate per
  fingerprint can slip through until the queue has drained once.

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

`ConfigureServer` is invoked once **per background job server**, so with
[`queueConfigurations`](#tuning-queues-for-high-volume-workers-and-prefetching) it runs for the shared
server and for every dedicated queue server. It runs last, which means a configurator that sets
`WorkerCount` or `Queues` overrides the per-queue configuration — use `options.Queues` to tell the
servers apart if you only want to touch one of them.

When a configurator supplies the storage (`IncludesStorage => true`, or by calling `UseStorage` in
`Configure`), the batch API falls back to creating jobs through the regular Hangfire client: the bulk
statement talks to the connection string from `hangfire:` configuration and must never be pointed at
someone else's storage.

All `IHangfireConfigurator` implementations are discovered automatically by the Dosaic plugin system.

## Observability

### Health check

A Hangfire readiness health check is registered automatically and is accessible via the standard Dosaic health endpoints (`/health/readiness`). It verifies that at least one Hangfire server is running.

### OpenTelemetry tracing

Hangfire jobs are automatically instrumented with OpenTelemetry tracing via `OpenTelemetry.Instrumentation.Hangfire`.

In addition, every `AsyncJob` / `ParameterizedAsyncJob<T>` body runs inside a wrapper span emitted on the shared `Dosaic` `ActivitySource` (`Tracing.SourceName`):

- **Span name** — `GetType().FullName` of the concrete job
- **Status** — set to `Ok` on success, `Error` (with the exception attached) on failure
- **Log scope** — `job.type` is also pushed onto the `ILogger` scope for the duration of the job

To enrich the span with business identifiers (e.g. tenant id, message id), override `EnrichActivity`:

```csharp
public class ProcessOrderJob : ParameterizedAsyncJob<OrderId>
{
    protected override void EnrichActivity(Activity activity, OrderId value)
    {
        activity?.SetTag("order.id", value.ToString());
    }

    protected override Task<object> ExecuteJobAsync(OrderId value, CancellationToken ct) { ... }
}
```

`AsyncJob` has the same hook with the single-argument `EnrichActivity(Activity activity)` signature. Default implementation is a no-op.

#### PostgreSQL polling-noise filter

Hangfire's PostgreSQL job-storage polls the `hangfire.*` schema continuously, which produces a high volume of low-value SQL spans on whatever database instrumentation is enabled (Npgsql, EF Core, etc.). `HangFirePlugin` registers `HangfireSqlNoiseProcessor` as the **first** OpenTelemetry processor in the pipeline; it inspects `db.statement` / `db.query.text` / `db.statement.text` plus the activity display/operation name and clears `ActivityTraceFlags.Recorded` on any span referencing the `hangfire` schema, so the OTLP batch exporter drops them before send. No configuration knob — the filter is on whenever the Hangfire plugin is active.

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
