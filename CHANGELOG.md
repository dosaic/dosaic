# Changelog

All Dosaic packages are released together under one version.

## 1.2.49

Everything in this release is opt-in: an unchanged configuration behaves exactly as in 1.2.48, with the
one exception listed under "Behaviour changes".

### Behaviour changes

- **`Dosaic.Plugins.Jobs.Hangfire` — connection string values are now escaped.**
  `HangfireConfiguration.ConnectionString` is built with `NpgsqlConnectionStringBuilder` instead of string
  interpolation. A password (or any other value) containing `;`, `=` or quotes used to break the string or
  inject connection parameters; it is now passed through verbatim. If you worked around this by
  pre-escaping a value in your configuration, remove the manual escaping. A configuration without such
  characters and without any of the new keys yields the byte-identical string as before; `port` is only
  written when it is greater than 0.
- **`Dosaic.Plugins.Jobs.Hangfire` — a missing schema is reported by the schema guard.** When the Hangfire
  schema does not exist at all, startup now fails with `Hangfire schema '<name>' has no version row - the
  schema was not prepared.` instead of a raw PostgreSQL "relation does not exist" error. This can only
  happen with the new `prepareSchemaIfNecessary: false`.

### New configuration keys

#### `Dosaic.Plugins.Jobs.Hangfire` — process roles

| Key | Default | Effect |
|---|---|---|
| `hangfire:server:enabled` | `true` | `false` starts no Hangfire server of any kind. Wins over `server:queues`. |
| `hangfire:server:queues` | `{}` | Map `queueName -> workerCount`. When not empty: one server per entry with a count above 0, no shared server, no implicit `default`; `0` drops a queue. |
| `hangfire:server:shutdownTimeoutInSeconds` | `null` | `BackgroundJobServerOptions.ShutdownTimeout`; `null` keeps Hangfire's default. |
| `hangfire:prepareSchemaIfNecessary` | `true` | `PostgreSqlStorageOptions.PrepareSchemaIfNecessary`. |
| `hangfire:registerRecurringJobs` | `true` | `false` skips `JobOptions.ConfigureJobs` and the `[RecurringJob]` scan. |
| `hangfire:healthCheck:enabled` | `true` | `false` registers no `hangfire` health check. |
| `hangfire:healthCheck:minimumAvailableServers` | `null` | `null` = 1 when the process starts a server, otherwise no server count check. |
| `hangfire:statisticsMetrics:enabled` | `true` | `false` does not start `HangfireStatisticsMetricsReporter`. |

#### `Dosaic.Plugins.Jobs.Hangfire` — PostgreSQL connection

| Key | Default | Effect |
|---|---|---|
| `hangfire:sslMode` | `""` (not set) | Npgsql `SslMode` name: `Disable`, `Allow`, `Prefer`, `Require`, `VerifyCA`, `VerifyFull`. |
| `hangfire:rootCertificate` | `""` (not set) | `RootCertificate`, path of the CA certificate file. |
| `hangfire:maxPoolSize` | `null` | `MaxPoolSize`; `null` keeps the Npgsql default (100). |
| `hangfire:noResetOnClose` | `false` | `NoResetOnClose`. |
| `hangfire:connectionParameters` | `""` | Raw Npgsql connection string, applied first; discrete keys override it. |

These keys apply to every PostgreSQL connection of the plugin: the Hangfire storage and Dosaic's own
batch, prefetch and uniqueness connections.

#### `Dosaic.Plugins.Persistence.EfCore.NpgSql`

| Key | Default | Effect |
|---|---|---|
| `npgsql:sslMode` | `""` (not set) | Npgsql `SslMode` name. |
| `npgsql:rootCertificate` | `""` (not set) | `RootCertificate`, path of the CA certificate file. |
| `npgsql:noResetOnClose` | `false` | `NoResetOnClose`. |
| `npgsql:connectionParameters` | `""` | Raw Npgsql connection string, applied first; the discrete settings, including their defaults, override it. |

All four are applied before the `WithDataSource` hook, so the hook still wins. The generated connection
string is unchanged while the keys are left on their defaults.

#### `Dosaic.Hosting.WebHost`

| Key | Default | Effect |
|---|---|---|
| `host:healthChecks:memory:maximumMegabytes` | `null` (2048) | Threshold of the `memory` liveness check. `0` does not register the check. |

#### `Dosaic.Plugins.Caching.Redis`

| Key | Default | Effect |
|---|---|---|
| `redisCache:instanceName` | `null` | `RedisCacheOptions.InstanceName`, the prefix of every cache key and of the rate limiter keys. Exposed on `RedisCacheConfiguration` for code that uses `IConnectionMultiplexer` directly. Ignored in in-memory mode. |
| `redisCache:rateLimitLeaseTtlInSeconds` | `120` | How long a permit of a distributed concurrency limiter stays taken once its holder stops renewing it (crashed process). Held permits are renewed three times per TTL, so calls may run longer than this. |

#### `Dosaic.Extensions.RestEase` — distributed rate limits

| Key | Default | Effect |
|---|---|---|
| `<section>:rateLimits:distributed` | `false` | `true` creates the limiters through the registered `IDistributedRateLimiterFactory`, so all processes share one budget. |
| `<section>:rateLimits:distributedKey` | client name | Identifies the shared budget. |

### New API

- `Dosaic.Extensions.Abstractions.RateLimiting.IDistributedRateLimiterFactory` (BCL types only) and
  `InProcessRateLimiterFactory`. The abstraction lives in `Dosaic.Extensions.Abstractions` rather than
  `Dosaic.Hosting.Abstractions`, so `Dosaic.Extensions.RestEase` stays usable outside the Dosaic host.
  `Dosaic.Extensions.RestEase` and `Dosaic.Plugins.Caching.Redis` now reference `Dosaic.Extensions.Abstractions`.
- `Dosaic.Plugins.Caching.Redis` registers a Redis backed factory (`useInMemory: false`) or the in-process
  one (`useInMemory: true`) unless the host registered its own. Redis keys are
  `{instanceName}ratelimit:{key}:{sliding|fixed|token|concurrency}`; the limiters need Redis 5 or newer.
- `distributed: true` without a registered factory throws when the client pipeline is built.

### Dependencies

- `System.Threading.RateLimiting` is now pinned centrally, which moves the version `Dosaic.Extensions.RestEase`
  resolves transitively from 8.0.0 to the `Microsoft.Extensions` version of the release.
