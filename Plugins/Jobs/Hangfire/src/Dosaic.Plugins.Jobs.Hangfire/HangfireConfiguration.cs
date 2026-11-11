using Dosaic.Hosting.Abstractions.Attributes;
using Hangfire.States;
using Npgsql;

namespace Dosaic.Plugins.Jobs.Hangfire
{
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

        /// <summary>
        ///     The connection string every PostgreSQL path of this plugin uses (storage, batching, prefetching,
        ///     uniqueness). Built with <see cref="NpgsqlConnectionStringBuilder" />, so values are escaped.
        ///     <see cref="ConnectionParameters" /> is applied first, the discrete keys override it.
        /// </summary>
        public string ConnectionString => BuildConnectionString();

        public int MaxJobArgumentsSizeToRenderInBytes { get; set; } = 4096;

        /// <summary>
        ///     Storage schema the Hangfire tables live in. Only relevant for the PostgreSQL storage.
        /// </summary>
        public string SchemaName { get; set; } = "hangfire";

        /// <summary>
        ///     How often a worker asks the storage for the next job when the queue was empty.
        ///     Hangfire's own default is 15 seconds, which is far too slow when the queues are used
        ///     as a message bus replacement.
        /// </summary>
        public int QueuePollIntervalInMs { get; set; } = 1000;

        /// <summary>
        ///     Keeps extending the invisibility timeout while a job is running instead of relying on a
        ///     single fixed window. Recommended when jobs run longer than <see cref="InvisibilityTimeoutInMinutes" />.
        /// </summary>
        public bool UseSlidingInvisibilityTimeout { get; set; }

        /// <summary>
        ///     Maximum number of jobs written per statement by the batch API.
        ///     0 (default) means "no limit" — the whole batch is written in exactly one round trip.
        ///     Set it to a positive number to trade round trips for smaller statements/parameter arrays.
        /// </summary>
        public int BatchChunkSize { get; set; }

        /// <summary>
        ///     Per queue tuning. Every entry gets its own background job server so that worker count and
        ///     prefetching can be tuned independently per queue. Queues that are not listed here are served
        ///     by the default server using <see cref="WorkerCount" />.
        /// </summary>
        public QueueConfiguration[] QueueConfigurations { get; set; } = [];

        /// <summary>
        ///     Npgsql <c>SslMode</c> name (Disable, Allow, Prefer, Require, VerifyCA, VerifyFull).
        ///     Empty (default) leaves Npgsql's own default in place.
        /// </summary>
        public string SslMode { get; set; }

        /// <summary>Path of the CA certificate file used to verify the server. Empty (default) = not set.</summary>
        public string RootCertificate { get; set; }

        /// <summary>Maximum connection pool size. Null (default) keeps Npgsql's default of 100.</summary>
        public int? MaxPoolSize { get; set; }

        /// <summary>
        ///     Skips the <c>DISCARD ALL</c> Npgsql sends when a connection returns to the pool. Required behind
        ///     pgBouncer in transaction mode.
        /// </summary>
        public bool NoResetOnClose { get; set; }

        /// <summary>
        ///     Optional raw Npgsql connection string. It is applied first, every discrete key
        ///     (host, port, database, user, password, sslMode, ...) that is set overrides it.
        /// </summary>
        public string ConnectionParameters { get; set; }

        /// <summary>Which background job servers this process starts.</summary>
        public HangfireServerConfiguration Server { get; set; } = new();

        /// <summary>
        ///     Lets Hangfire.PostgreSql create or upgrade its schema when the storage is built.
        ///     Disable it for processes that must not run DDL - the schema guard then fails startup when
        ///     the schema is missing.
        /// </summary>
        public bool PrepareSchemaIfNecessary { get; set; } = true;

        /// <summary>
        ///     Registers recurring jobs at startup: <see cref="JobOptions.ConfigureJobs" /> and every
        ///     <see cref="Attributes.RecurringJobAttribute" /> job.
        /// </summary>
        public bool RegisterRecurringJobs { get; set; } = true;

        /// <summary>The readiness health check of this plugin.</summary>
        public HangfireHealthCheckConfiguration HealthCheck { get; set; } = new();

        /// <summary>The storage statistics exported as metrics.</summary>
        public HangfireStatisticsMetricsConfiguration StatisticsMetrics { get; set; } = new();

        private string BuildConnectionString()
        {
            NpgsqlConnectionStringBuilder builder;
            try
            {
                builder = new NpgsqlConnectionStringBuilder(ConnectionParameters ?? "");
            }
            catch (Exception e)
            {
                // the raw value may carry credentials, so neither it nor the inner message is repeated here
                throw new InvalidOperationException(
                    $"Configuration: hangfire:connectionParameters is not a valid Npgsql connection string ({e.GetType().Name}).");
            }

            if (!string.IsNullOrEmpty(Host)) builder.Host = Host;
            if (Port > 0) builder.Port = Port;
            if (!string.IsNullOrEmpty(Database)) builder.Database = Database;
            if (!string.IsNullOrEmpty(User)) builder.Username = User;
            if (!string.IsNullOrEmpty(Password)) builder.Password = Password;
            if (!string.IsNullOrWhiteSpace(SslMode)) builder.SslMode = ParseSslMode(SslMode);
            if (!string.IsNullOrWhiteSpace(RootCertificate)) builder.RootCertificate = RootCertificate;
            if (MaxPoolSize.HasValue) builder.MaxPoolSize = MaxPoolSize.Value;
            if (NoResetOnClose) builder.NoResetOnClose = true;
            // the interpolated string this replaced ended with a separator; keeping it means a configuration
            // without special characters or new keys yields the exact same string (and Npgsql pool) as before
            var connectionString = builder.ConnectionString;
            return connectionString.Length == 0 ? connectionString : connectionString + ";";
        }

        private static Npgsql.SslMode ParseSslMode(string value)
        {
            var name = value.Trim();
            // Enum.TryParse also accepts numbers, which would silently pick a mode nobody wrote down
            if (name.All(char.IsLetter) && Enum.TryParse<Npgsql.SslMode>(name, true, out var sslMode))
                return sslMode;
            throw new InvalidOperationException(
                $"Configuration: hangfire:sslMode '{value}' is not a valid Npgsql SslMode. " +
                $"Expected one of: {string.Join(", ", Enum.GetNames<Npgsql.SslMode>())}.");
        }
    }

    public class HangfireServerConfiguration
    {
        /// <summary>
        ///     False starts no background job server of any kind in this process. Enqueueing, recurring job
        ///     registration and the dashboard keep working. Wins over <see cref="Queues" />.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Queue name to worker count. Empty (default) keeps the layout derived from
        ///     <see cref="HangfireConfiguration.Queues" />, <see cref="HangfireConfiguration.WorkerCount" /> and
        ///     <see cref="HangfireConfiguration.QueueConfigurations" />. When it has entries, every entry with a
        ///     count above 0 gets its own server and nothing else is served - no shared server, no implicit
        ///     <c>default</c> queue. An entry with 0 is skipped, which lets a later configuration layer drop a queue.
        /// </summary>
        public Dictionary<string, int> Queues { get; set; } = [];

        /// <summary>
        ///     How long a server waits for running jobs when the host stops. Null (default) keeps Hangfire's default.
        /// </summary>
        public int? ShutdownTimeoutInSeconds { get; set; }
    }

    public class HangfireHealthCheckConfiguration
    {
        /// <summary>False registers no "hangfire" health check.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Number of servers that have to be registered in the storage for the check to be healthy.
        ///     Null (default) means 1 when this process starts a server, and no server count check otherwise.
        /// </summary>
        public int? MinimumAvailableServers { get; set; }
    }

    public class HangfireStatisticsMetricsConfiguration
    {
        /// <summary>
        ///     False does not start the reporter that exports the storage wide job statistics. The numbers are
        ///     global, so one exporting process per installation is enough.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    public class QueueConfiguration
    {
        /// <summary>Name of the queue this configuration applies to.</summary>
        public string Name { get; set; }

        /// <summary>Number of workers dedicated to this queue. Falls back to the global worker count.</summary>
        public int? WorkerCount { get; set; }

        /// <summary>
        ///     Number of jobs fetched from the queue per polling round trip. 1 (default) keeps Hangfire's
        ///     stock one-job-per-query behaviour. Higher values amortize the fetch cost over many jobs and
        ///     are the main throughput lever for high volume queues. Only supported on PostgreSQL storage.
        /// </summary>
        public int PrefetchCount { get; set; } = 1;

        /// <summary>How often this queue is polled when it ran empty. Falls back to <see cref="HangfireConfiguration.QueuePollIntervalInMs" />.</summary>
        public int? QueuePollIntervalInMs { get; set; }
    }
}
