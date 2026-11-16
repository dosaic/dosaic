# Changelog

All Dosaic packages are released together under one version.

## Unreleased (next: 1.2.53)

### Fixes

- **`Dosaic.Plugins.Jobs.Hangfire` — a hidden dashboard answers 404 instead of 500.** With
  `hangfire:dashboard:hideWhenDenied: true`, a denied request set 404 and `Content-Length: 0` without starting the
  response. The `ExceptionMiddleware` then wrote its `ErrorResponse` body for the 404, Kestrel refused the write
  (`Response Content-Length mismatch: too many bytes written`) and the client got 500. The gate now only sets
  the status code, like a request that matches no endpoint. A denied request gets the same 404 as any unknown
  path: with the default `ExceptionMiddleware` that is the `ErrorResponse` JSON body, without it an empty body.

## Unreleased (next: 1.2.52)

An application that sets none of the new keys and implements no `ConfigureDashboard` behaves as in 1.2.51, with
the exceptions listed under "Behaviour changes".

### Behaviour changes

- **`Dosaic.Hosting.WebHost` — forwarded headers from private networks are applied.** The web host enabled all
  forwarded headers but kept ASP.NET Core's trust list, which only accepts them from loopback. Behind an
  ingress controller in Kubernetes they were silently ignored: `RemoteIpAddress` was the ingress pod, `Scheme`
  stayed `http`, and `X-Forwarded-Prefix` never reached `PathBase`. Now they are accepted from loopback and the
  private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `100.64.0.0/10`, `fc00::/7`) and the
  `X-Forwarded-For` chain is walked back through trusted proxies (`ForwardLimit` was 1). Visible effects
  behind a proxy: the client IP is the real client (IP rate limiting and request logs count per client instead
  of per ingress pod), `Request.Scheme` is `https`, and generated links carry the forwarded prefix. Restrict
  `host:forwardedHeaders:knownNetworks` when other workloads in the cluster can reach the pod and must not set
  these headers.

- **`Dosaic.Plugins.Jobs.Hangfire` — the dashboard is an endpoint.** `/hangfire` is mapped with
  `MapHangfireDashboard` in `ConfigureEndpoints` instead of `UseHangfireDashboard` in `ConfigureApplication`.
  Endpoints execute after every middleware, so the dashboard now always runs after `UseAuthentication` and
  `UseAuthorization`; before, the order depended on the undefined order of the Dosaic plugins. The endpoint
  allows anonymous access, so a fallback authorization policy no longer challenges dashboard requests; the
  dashboard's own filters decide, as before. Middlewares that plugins add after the Hangfire plugin now also see
  dashboard requests. Path, filters, status codes and pages are unchanged.

### New configuration keys

| Key | Default | Effect |
|---|---|---|
| `hangfire:dashboard:auth` | `host` | `host` checks `allowedDashboardHost` (as before); `role` lets authenticated users in `hangfire:dashboard:role` in, on any host. |
| `hangfire:dashboard:role` | `""` | Role for `auth: role`; required in that mode. |
| `host:forwardedHeaders:knownNetworks` | loopback + private ranges | CIDR networks whose forwarded headers are applied, separated by `,` or `;`. |
| `host:forwardedHeaders:knownProxies` | `""` | Single trusted proxy addresses in addition. |
| `host:forwardedHeaders:forwardLimit` | `null` | Maximum proxy hops; `null` walks back through trusted proxies. |
| `hangfire:dashboard:hideWhenDenied` | `false` | `true` answers 404 with an empty body instead of 401/403 when a dashboard authorization filter refuses, on every path below `/hangfire`. |
| `zitadel:cookieToken:name` | `""` | Cookie the "ZITADEL" scheme reads the access token from when a request has no `Authorization` header. |
| `zitadel:cookieToken:paths` | `[]` | Paths (after `PathBase`, per segment) on which the cookie is read; ignored everywhere else. Required with `name`, `/` is refused. |
| `zitadel:mapProjectRoles` | `false` | `true` adds a `ClaimTypes.Role` claim per key of `urn:zitadel:iam:org:project:roles` and `urn:zitadel:iam:org:project:<projectId>:roles`. |

### New API

- `Dosaic.Plugins.Jobs.Hangfire`: `IHangfireConfigurator.ConfigureDashboard(DashboardOptions)`, a default
  interface method called after the plugin's defaults. It can replace `Authorization` / `AsyncAuthorization`
  (and so drop the `allowedDashboardHost` check), set `IgnoreAntiforgeryToken`, `AppPath` and similar.
  `HangFirePlugin.DashboardPath` (`/hangfire`) and `HangfireConfiguration.Dashboard`.
- `Dosaic.Plugins.Authorization.Zitadel`: `ZitadelConfiguration.CookieToken`
  (`ZitadelCookieTokenConfiguration`) and `ZitadelConfiguration.MapProjectRoles`.

## Unreleased (next: 1.2.50)

An application that sets `telemetry:endpoint` and valid `npgsql` connection keys behaves as in 1.2.49, with
the exceptions listed under "Behaviour changes".

### Behaviour changes

- **`Dosaic.Hosting.WebHost` — no OTLP metric export without `telemetry:endpoint`.** Traces and logs were
  already exported only when `telemetry:endpoint` is set. Metrics always got an OTLP exporter, which sent
  to `OTEL_EXPORTER_OTLP_ENDPOINT` or to the OpenTelemetry default `http://localhost:4317`. Now no signal
  gets an OTLP exporter without `telemetry:endpoint`, and `OTEL_EXPORTER_OTLP_ENDPOINT` alone turns
  nothing on. The Prometheus endpoint `/metrics` and all metric instrumentation are unchanged. If you
  relied on `OTEL_EXPORTER_OTLP_ENDPOINT` for metrics, set `TELEMETRY_ENDPOINT` instead.
- **`Dosaic.Plugins.Persistence.EfCore.NpgSql` — invalid connection keys stop the start.** The new
  `EfCoreNpgSqlPlugin` builds the connection string from the `npgsql` section when the host starts. An
  unknown `npgsql:sslMode` or an unparsable `npgsql:connectionParameters` now stops the host instead of
  failing the first DbContext, the same as `hangfire:sslMode` and `hangfire:connectionParameters`. The
  `rootCertificate` file is not checked.

- **`Dosaic.Hosting.WebHost` — OTLP over HTTP posts to the signal paths.** With `telemetry:protocol:
  http/protobuf` traces, metrics and logs were all posted to the root of `telemetry:endpoint`, which an OTLP
  collector rejects. They now go to `<endpoint>/v1/traces`, `/v1/metrics` and `/v1/logs`, as the OTLP
  specification and `OTEL_EXPORTER_OTLP_ENDPOINT` define; a base path of the endpoint is kept. gRPC is
  unchanged. If you pointed `telemetry:endpoint` at one signal path as a workaround, set the base URL instead.
- **`Dosaic.Plugins.Persistence.EfCore.NpgSql` — `npgsql:host`, `npgsql:database` and `npgsql:username` are
  required.** They may also come from `Host`, `Database` and `Username` in `npgsql:connectionParameters`. A
  section without one of them stops the host at startup, naming the missing keys and their environment
  variables. Such a host failed at the first database access before.
- **`Dosaic.Plugins.Persistence.S3` — the bucket migration log counts right.** The migration still makes 4
  attempts by default. Each failure is logged as `(attempt n of 4)`, and the last line says
  `after 4 attempts -> giving up` instead of `after 3 attempts`. `BlobStorageBucketMigrationService` takes an
  `IHostApplicationLifetime`, which every host registers.

### New configuration keys

| Key | Default | Effect |
|---|---|---|
| `s3:healthCheck:enabled` | `true` | `false` registers no `s3` or `s3-local-filesystem` health check. |
| `s3:healthCheck:bucket` | `""` | When set, the `s3` check sends `HEAD <bucketPrefix><bucket>` with the configured credentials instead of the anonymous URL check. |
| `s3:bucketMigration:maxAttempts` | `4` | Attempts of the bucket migration, `0` = until the host stops. |
| `s3:bucketMigration:stopHostOnFailure` | `false` | `true` stops the host with exit code 1 when the buckets cannot be created. |
| `npgsql:migration:maxAttempts` | `0` | Attempts of `NpgsqlDbMigratorService`, `0` = until the host stops (as before). |
| `npgsql:migration:stopHostOnFailure` | `false` | `true` stops the host with exit code 1 when the migration fails. |

### New API

- `Dosaic.Hosting.Abstractions`: `StartupRetryOptions` and `StartupRetry.RunAsync` run a startup task with a
  limited number of attempts and can stop the host with exit code 1. `ConfigurationAttribute.RequiredKeys`
  names the keys a section cannot do without.
- `Dosaic.Plugins.Persistence.EfCore.Abstractions`: `AddDbMigratorService<TDbContext>(StartupRetryOptions)`.

### Fixes

- `telemetry:protocol` accepts `http/protobuf` and `grpc`, the values of `OTEL_EXPORTER_OTLP_PROTOCOL` that the
  README always listed. Only the enum names `Grpc` and `HttpProtobuf` worked; `http/protobuf` stopped the start
  with a binder error. An unknown value now stops the start with a message naming `telemetry:protocol`.
- A missing configuration section for a `[Configuration]` class stopped the start with `No node deserializer
  was able to deserialize the node`. The message now names the section, the class and its required keys, for
  example `Configuration section 'npgsql' is missing. EfCoreNpgSqlConfiguration is bound from it. Set at least
  npgsql:host, npgsql:database, npgsql:username (environment: NPGSQL_HOST, NPGSQL_DATABASE, NPGSQL_USERNAME).`
- `npgsql:sslMode` and `hangfire:sslMode` accept the libpq names that Helm charts and CloudNativePG use:
  `disable`, `allow`, `prefer`, `require`, `verify-ca`, `verify-full`, in any casing.
  `ConnectionStringSettings.ParseEnumName` lets a hyphen separate the words of an enum name, and its error
  message says so.

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
| `npgsql:connectionParameters` | `""` | Raw Npgsql connection string, applied first; configured discrete settings override it, the defaults of `port`, `connectionLifetime`, `keepAlive`, `maxPoolSize` and `includeErrorDetail` only fill in what it leaves unset. Same rules as `hangfire:connectionParameters`. |

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

- `--write-config-schema [path]` (or `PluginWebHostBuilder.WriteConfigurationSchema`) writes a JSON schema
  of the configuration of the installed plugins, for completion and validation of `appsettings.*` files in
  editors, and exits. The environment variable `DOSAIC_WRITE_CONFIG_SCHEMA=<path>` writes it and carries
  on, so another export can run in the same process. See the WebHost README.
- All packages now ship their XML documentation, which is where the schema takes its descriptions from.
- `Dosaic.Hosting.Abstractions.Configuration.HostConfiguration` types the whole `host` section (`urls`,
  `maxRequestSize`, `additionalConfigPaths`, `healthChecks:memory:maximumMegabytes`). The host reads it once,
  registers it as a singleton and passes the same instance to plugin constructors. Key names and defaults
  are unchanged.
- `Dosaic.Extensions.Abstractions.RateLimiting.IDistributedRateLimiterFactory` (BCL types only) and
  `InProcessRateLimiterFactory`. The abstraction lives in `Dosaic.Extensions.Abstractions` rather than
  `Dosaic.Hosting.Abstractions`, so `Dosaic.Extensions.RestEase` stays usable outside the Dosaic host.
  `Dosaic.Extensions.RestEase` and `Dosaic.Plugins.Caching.Redis` now reference `Dosaic.Extensions.Abstractions`.
- `Dosaic.Plugins.Caching.Redis` registers a Redis backed factory (`useInMemory: false`) or the in-process
  one (`useInMemory: true`) unless the host registered its own. Redis keys are
  `{instanceName}ratelimit:{key}:{sliding|fixed|token|concurrency}`; the limiters need Redis 5 or newer.
- `distributed: true` without a registered factory throws when the client pipeline is built.
- Redis errors never fail a call of a distributed limiter: the call is decided by the in-process limiter.
  Only 3 failures in a row stop asking Redis for 5 seconds, and errors that do not go away on their own
  (`WRONGTYPE`, `READONLY`, `OOM`, script errors) are logged as errors. See the Redis README.
- `Dosaic.Hosting.Abstractions.Configuration.ConnectionStringSettings` builds a connection string from a raw
  `connectionParameters` value and discrete keys with the rules both PostgreSQL plugins use.

### Fixes

- Dictionaries in configuration classes bind when every key is a number (`"1": 4`) — the section used to be
  read as a list and binding failed. Below a dictionary of plain values, keys that environment variables
  split at `_` are joined back, so `HANGFIRE_SERVER_QUEUES_MY_QUEUE=3` serves `my_queue`.
- `DOSAIC_WRITE_CONFIG_SCHEMA` does not rewrite an unchanged schema and reports a schema it cannot write
  instead of failing the start. The host's `args` and the process arguments are read separately for
  `--write-config-schema`. Enum values are accepted in any casing and as numbers by the schema, and a
  `NaN`, infinite or huge `double` default no longer breaks the export.

### Dependencies

- `System.Threading.RateLimiting` is now pinned centrally, which moves the version `Dosaic.Extensions.RestEase`
  resolves transitively from 8.0.0 to the `Microsoft.Extensions` version of the release.
