using System.Text.RegularExpressions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Fetching;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Newtonsoft.Json;
using Npgsql;
using OpenTelemetry.Trace;
using BindingFlags = System.Reflection.BindingFlags;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    public class HangFirePlugin : IPluginServiceConfiguration, IPluginApplicationConfiguration,
        IPluginHealthChecksConfiguration
    {
        private readonly IImplementationResolver _implementationResolver;
        private readonly IHangfireConfigurator[] _configurators;
        private readonly HangfireConfiguration _hangfireConfig;

        /// <summary>
        ///     The storage this plugin built itself, or null when storage comes from somewhere else.
        ///     Only a storage we own may be written to by the bulk dispatcher, which talks to the configured
        ///     connection string directly.
        /// </summary>
        private JobStorage _managedStorage;

        /// <summary>
        ///     Built once - every connection has to use the very same string to end up in the same Npgsql pool.
        /// </summary>
        private readonly Lazy<string> _connectionString;

        /// <summary>Same character set Hangfire accepts for queue names.</summary>
        private static readonly Regex _queueName = new("^[a-z0-9_-]+$", RegexOptions.Compiled);

        internal const string ServerQueuesKey = "hangfire:server:queues";

        public HangFirePlugin(HangfireConfiguration configuration, IImplementationResolver implementationResolver,
            IHangfireConfigurator[] configurators)
        {
            _implementationResolver = implementationResolver;
            _configurators = configurators;
            _hangfireConfig = configuration;
            _connectionString = new Lazy<string>(() => _hangfireConfig.ConnectionString);
        }

        private HangfireServerConfiguration ServerConfig => _hangfireConfig.Server ?? new HangfireServerConfiguration();

        private HangfireHealthCheckConfiguration HealthCheckConfig =>
            _hangfireConfig.HealthCheck ?? new HangfireHealthCheckConfiguration();

        private bool BuildsPostgresStorage => !_hangfireConfig.InMemory && _configurators.All(x => !x.IncludesStorage);

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            // fail on a broken connection configuration here instead of on the first connection
            if (BuildsPostgresStorage) _ = _connectionString.Value;
            serviceCollection.AddFeatureManagement();
            serviceCollection.AddHangfire(conf =>
                {
                    conf.UseSerializerSettings(new JsonSerializerSettings());
                    conf.UseMaxArgumentSizeToRender(_hangfireConfig.MaxJobArgumentsSizeToRenderInBytes);
                    if (_hangfireConfig.InMemory)
                        conf.UseMemoryStorage();
                    else if (BuildsPostgresStorage)
                        conf.UseStorage(CreatePostgresStorage());
                    conf.UseSimpleAssemblyNameTypeSerializer();
                    _configurators.ForEach(x => x.Configure(conf));
                }
            );
            ConfigureServers(serviceCollection);
            serviceCollection.AddSingleton<IJobBatchDispatcher>(sp =>
            {
                // resolving the storage first is what runs the AddHangfire callback above, and therefore what
                // assigns _managedStorage — checking the field before that would always read null
                var storage = sp.GetRequiredService<JobStorage>();
                // a configurator may still have replaced the storage after ours was registered — in that case
                // we must not bulk write to the connection string from our own configuration
                var ownsStorage = _managedStorage is not null && ReferenceEquals(storage, _managedStorage);
                return ownsStorage
                    ? new PostgresJobBatchDispatcher(CreateConnection, _hangfireConfig.SchemaName,
                        _hangfireConfig.BatchChunkSize)
                    : new BackgroundJobClientBatchDispatcher(sp.GetRequiredService<IBackgroundJobClient>());
            });
            serviceCollection.AddSingleton<IJobManager>(sp =>
            {
                var jobStorage = sp.GetRequiredService<JobStorage>();
                var recurringJobManager = sp.GetRequiredService<IRecurringJobManager>();
                var backgroundJobClient = sp.GetRequiredService<IBackgroundJobClient>();
                var batchDispatcher = sp.GetRequiredService<IJobBatchDispatcher>();
                var hasQueueSupport = jobStorage.HasFeature(JobStorageFeatures.JobQueueProperty);
                return new JobManager(hasQueueSupport, jobStorage.GetConnection(), jobStorage.GetMonitoringApi(),
                    recurringJobManager, backgroundJobClient, batchDispatcher);
            });
            if (_hangfireConfig.StatisticsMetrics?.Enabled ?? true)
                serviceCollection.AddHostedService<HangfireStatisticsMetricsReporter>();
            serviceCollection.AddOpenTelemetry().WithTracing(builder => builder.AddHangfireInstrumentation());
            // Hangfire's PostgreSQL polling generates a large volume of low-value
            // SQL spans. Drop them before the OTLP batch exporter runs.
            HangfireSqlNoiseProcessor.RegisterFirst(serviceCollection);
        }

        private NpgsqlConnection CreateConnection() => new(_connectionString.Value);

        internal PostgreSqlStorageOptions CreateStorageOptions() => new()
        {
            InvisibilityTimeout = TimeSpan.FromMinutes(_hangfireConfig.InvisibilityTimeoutInMinutes),
            SchemaName = _hangfireConfig.SchemaName,
            QueuePollInterval = TimeSpan.FromMilliseconds(_hangfireConfig.QueuePollIntervalInMs),
            UseSlidingInvisibilityTimeout = _hangfireConfig.UseSlidingInvisibilityTimeout,
            PrepareSchemaIfNecessary = _hangfireConfig.PrepareSchemaIfNecessary
        };

        private JobStorage CreatePostgresStorage()
        {
            PostgresSchemaGuard.ValidateName(_hangfireConfig.SchemaName);
            var storageOptions = CreateStorageOptions();
            var storage = new PostgreSqlStorage(
                new NpgsqlConnectionFactory(_connectionString.Value, storageOptions), storageOptions);
            var prefetchQueues = GetQueueConfigurations().Where(x => x.PrefetchCount > 1).ToArray();
            if (prefetchQueues.Length == 0) return _managedStorage = storage;
            var invisibilityTimeout = TimeSpan.FromMinutes(_hangfireConfig.InvisibilityTimeoutInMinutes);
            var slidingInterval = _hangfireConfig.UseSlidingInvisibilityTimeout
                ? invisibilityTimeout / 2
                : (TimeSpan?)null;
            var defaults = new PrefetchSettings
            {
                PrefetchCount = 1,
                PollInterval = TimeSpan.FromMilliseconds(_hangfireConfig.QueuePollIntervalInMs),
                InvisibilityTimeout = invisibilityTimeout,
                SlidingKeepAliveInterval = slidingInterval
            };
            var perQueue = prefetchQueues.ToDictionary(x => x.Name, x => new PrefetchSettings
            {
                PrefetchCount = x.PrefetchCount,
                PollInterval =
                    TimeSpan.FromMilliseconds(x.QueuePollIntervalInMs ?? _hangfireConfig.QueuePollIntervalInMs),
                InvisibilityTimeout = invisibilityTimeout,
                SlidingKeepAliveInterval = slidingInterval
            });
            return _managedStorage = new PrefetchJobStorage(storage,
                new PostgresJobQueueClient(CreateConnection, _hangfireConfig.SchemaName), perQueue, defaults);
        }

        private QueueConfiguration[] GetQueueConfigurations() =>
            (_hangfireConfig.QueueConfigurations ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name)
            .Select(x => x.First())
            .ToArray();

        private void ConfigureServers(IServiceCollection serviceCollection)
        {
            foreach (var server in GetServerLayout())
                AddServer(serviceCollection, server.Queues, server.WorkerCount, server.ServerNameSuffix);
        }

        /// <summary>
        ///     The background job servers this process starts. Empty when the server is switched off, or when
        ///     every entry of the queue map is 0.
        /// </summary>
        internal IReadOnlyList<ServerLayout> GetServerLayout()
        {
            if (!ServerConfig.Enabled) return [];
            var queueMap = GetServerQueueMap();
            if (queueMap.Count > 0)
                return queueMap.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new ServerLayout([x.Key], x.Value, x.Key)).ToList();

            var servers = new List<ServerLayout>();
            var dedicated = GetQueueConfigurations();
            var dedicatedNames = dedicated.Select(x => x.Name).ToHashSet();
            var sharedQueues = (_hangfireConfig.Queues ?? []).Union([EnqueuedState.DefaultQueue])
                .Distinct().Where(x => !dedicatedNames.Contains(x)).ToArray();
            if (sharedQueues.Length > 0)
                servers.Add(new ServerLayout(sharedQueues, _hangfireConfig.WorkerCount, null));
            servers.AddRange(dedicated.Select(queue =>
                new ServerLayout([queue.Name], queue.WorkerCount ?? _hangfireConfig.WorkerCount, queue.Name)));
            return servers;
        }

        /// <summary>
        ///     <c>hangfire:server:queues</c> with trimmed, lower-cased names. Keys that come from environment
        ///     variables arrive upper-case, while Hangfire only accepts lower-case queue names.
        /// </summary>
        private Dictionary<string, int> GetServerQueueMap()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (key, workerCount) in ServerConfig.Queues ?? [])
            {
                var name = (key ?? "").Trim().ToLowerInvariant();
                if (!_queueName.IsMatch(name))
                    throw new InvalidOperationException(
                        $"Configuration: {ServerQueuesKey}:{key} is not a valid Hangfire queue name. " +
                        "Queue names consist of lower-case letters, digits, underscores and dashes.");
                if (workerCount < 0)
                    throw new InvalidOperationException(
                        $"Configuration: {ServerQueuesKey}:{key} must be a worker count of 0 (not served) or more, " +
                        $"but is {workerCount}.");
                if (!result.TryAdd(name, workerCount))
                    throw new InvalidOperationException(
                        $"Configuration: {ServerQueuesKey}:{key} names the queue '{name}' a second time.");
            }

            return result;
        }

        internal record ServerLayout(string[] Queues, int? WorkerCount, string ServerNameSuffix);

        private void AddServer(IServiceCollection serviceCollection, string[] queues, int? workerCount,
            string serverNameSuffix)
        {
            serviceCollection.AddHangfireServer(options =>
            {
                options.Queues = queues;
                if (serverNameSuffix is not null)
                    options.ServerName =
                        $"{Environment.MachineName}:{Environment.ProcessId}:{serverNameSuffix}";
                if (_hangfireConfig.PollingIntervalInMs.HasValue)
                    options.SchedulePollingInterval =
                        TimeSpan.FromMilliseconds(_hangfireConfig.PollingIntervalInMs.Value);
                if (workerCount.HasValue)
                    options.WorkerCount = workerCount.Value;
                if (ServerConfig.ShutdownTimeoutInSeconds.HasValue)
                    options.ShutdownTimeout = TimeSpan.FromSeconds(ServerConfig.ShutdownTimeoutInSeconds.Value);
                _configurators.ForEach(x => x.ConfigureServer(options));
            });
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            var loggerFactory = applicationBuilder.ApplicationServices.GetRequiredService<ILoggerFactory>();
            GlobalJobFilters.Filters.Add(new LogJobExecutionFilter(loggerFactory));
            LogServerLayout(loggerFactory.CreateLogger<HangFirePlugin>());
            if (_hangfireConfig.EnableJobsByFeatureManagementConfig)
            {
                GlobalJobFilters.Filters.Add(new EnabledByFeatureFilter(
                    applicationBuilder.ApplicationServices.GetRequiredService<IFeatureManager>(),
                    applicationBuilder.ApplicationServices.GetRequiredService<ILogger<EnabledByFeatureFilter>>()));
            }

            RegisterUniquenessStore(applicationBuilder.ApplicationServices.GetRequiredService<JobStorage>());

            var urlAuthFilter = new HostAuthorizationFilter(_hangfireConfig.AllowedDashboardHost);
            applicationBuilder.UseHangfireDashboard(options: new()
            {
                Authorization = new List<IDashboardAuthorizationFilter> { urlAuthFilter }
            });
            if (!_hangfireConfig.RegisterRecurringJobs) return;
            var jobRegister = applicationBuilder.ApplicationServices.GetRequiredService<IJobManager>();
            var jobOptions = applicationBuilder.ApplicationServices.GetService<JobOptions>();
            jobOptions?.ConfigureJobs.Invoke(jobRegister, applicationBuilder.ApplicationServices);
            var registerJobMethod = jobRegister.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(x => x.Name == nameof(IJobManager.RegisterRecurring) && x.GetGenericArguments().Length == 1);
            foreach (var job in _implementationResolver.FindTypes(f =>
                         f.HasAttribute<RecurringJobAttribute>() && f.Implements<IAsyncJob>()))
            {
                if (job.IsGenericType) continue;
                var recurringJobDetails = job.GetAttribute<RecurringJobAttribute>()!;
                var genericMethod = registerJobMethod.MakeGenericMethod(job);
                genericMethod.Invoke(jobRegister, [recurringJobDetails.CronPattern, recurringJobDetails.Queue, ""]);
            }
        }

        private void LogServerLayout(ILogger logger)
        {
            if (!ServerConfig.Enabled)
            {
                logger.LogInformation("Hangfire server is disabled, this process only enqueues jobs");
                return;
            }

            if (GetServerLayout().Count == 0)
                logger.LogWarning(
                    "Every queue in {ConfigurationKey} has a worker count of 0, no Hangfire server is started",
                    ServerQueuesKey);
        }

        /// <summary>
        ///     Lets <see cref="UniquePerQueueAttribute" /> claim fingerprints with a single upsert instead of
        ///     the storage agnostic lock-and-read fallback. Only valid for a storage we built ourselves,
        ///     because the claim talks to the connection string from our own configuration.
        /// </summary>
        private void RegisterUniquenessStore(JobStorage jobStorage)
        {
            if (_managedStorage is null || !ReferenceEquals(jobStorage, _managedStorage)) return;
            // batching and prefetching write Hangfire.PostgreSql's private tables directly, so a schema this
            // plugin was not written against has to stop the host instead of corrupting job state
            PostgresSchemaGuard.AssertSupportedVersion(CreateConnection, _hangfireConfig.SchemaName);
            JobUniquenessStores.Use(jobStorage,
                new PostgresJobUniquenessStore(CreateConnection, _hangfireConfig.SchemaName));
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            if (!HealthCheckConfig.Enabled) return;
            var minimumAvailableServers = GetMinimumAvailableServers();
            healthChecksBuilder.AddHangfire(s => s.MinimumAvailableServers = minimumAvailableServers, "hangfire",
                tags: [HealthCheckTag.Readiness.Value]);
        }

        /// <summary>
        ///     A process that starts no server must not wait for one: it would never get ready while no other
        ///     process serves, and a fresh installation would deadlock on its own readiness.
        /// </summary>
        internal int? GetMinimumAvailableServers() =>
            HealthCheckConfig.MinimumAvailableServers ?? (GetServerLayout().Count > 0 ? 1 : null);

        internal class HostAuthorizationFilter : IDashboardAuthorizationFilter
        {
            private readonly string _allowedHost;

            public HostAuthorizationFilter(string allowedHost)
            {
                _allowedHost = (allowedHost ?? "").ToLowerInvariant();
            }

            public bool Authorize(DashboardContext context)
            {
                if (string.IsNullOrWhiteSpace(_allowedHost)) return false;
                var incomingRequest = context.GetHttpContext().Request;
                var incomingHost = incomingRequest.Host.Host.ToLowerInvariant();
                return incomingHost == _allowedHost;
            }
        }
    }
}
