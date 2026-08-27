using Dosaic.Hosting.Abstractions.Attributes;
using Hangfire.States;

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
        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={User};Password={Password};";
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
