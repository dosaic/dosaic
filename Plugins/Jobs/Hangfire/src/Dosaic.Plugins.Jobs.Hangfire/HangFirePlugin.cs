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

        public HangFirePlugin(HangfireConfiguration configuration, IImplementationResolver implementationResolver,
            IHangfireConfigurator[] configurators)
        {
            _implementationResolver = implementationResolver;
            _configurators = configurators;
            _hangfireConfig = configuration;
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddFeatureManagement();
            serviceCollection.AddHangfire(conf =>
                {
                    conf.UseSerializerSettings(new JsonSerializerSettings());
                    conf.UseMaxArgumentSizeToRender(_hangfireConfig.MaxJobArgumentsSizeToRenderInBytes);
                    if (_hangfireConfig.InMemory)
                        conf.UseMemoryStorage();
                    else if (_configurators.All(x => !x.IncludesStorage))
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
            serviceCollection.AddHostedService<HangfireStatisticsMetricsReporter>();
            serviceCollection.AddOpenTelemetry().WithTracing(builder => builder.AddHangfireInstrumentation());
            // Hangfire's PostgreSQL polling generates a large volume of low-value
            // SQL spans. Drop them before the OTLP batch exporter runs.
            HangfireSqlNoiseProcessor.RegisterFirst(serviceCollection);
        }

        private NpgsqlConnection CreateConnection() => new(_hangfireConfig.ConnectionString);

        private JobStorage CreatePostgresStorage()
        {
            PostgresSchemaGuard.ValidateName(_hangfireConfig.SchemaName);
            var storageOptions = new PostgreSqlStorageOptions
            {
                InvisibilityTimeout = TimeSpan.FromMinutes(_hangfireConfig.InvisibilityTimeoutInMinutes),
                SchemaName = _hangfireConfig.SchemaName,
                QueuePollInterval = TimeSpan.FromMilliseconds(_hangfireConfig.QueuePollIntervalInMs),
                UseSlidingInvisibilityTimeout = _hangfireConfig.UseSlidingInvisibilityTimeout
            };
            var storage = new PostgreSqlStorage(
                new NpgsqlConnectionFactory(_hangfireConfig.ConnectionString, storageOptions), storageOptions);
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
            var dedicated = GetQueueConfigurations();
            var dedicatedNames = dedicated.Select(x => x.Name).ToHashSet();
            var sharedQueues = _hangfireConfig.Queues.Union([EnqueuedState.DefaultQueue])
                .Distinct().Where(x => !dedicatedNames.Contains(x)).ToArray();
            if (sharedQueues.Length > 0)
                AddServer(serviceCollection, sharedQueues, _hangfireConfig.WorkerCount, null);
            foreach (var queue in dedicated)
                AddServer(serviceCollection, [queue.Name], queue.WorkerCount ?? _hangfireConfig.WorkerCount,
                    queue.Name);
        }

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
                _configurators.ForEach(x => x.ConfigureServer(options));
            });
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            GlobalJobFilters.Filters.Add(
                new LogJobExecutionFilter(applicationBuilder.ApplicationServices.GetRequiredService<ILoggerFactory>()));
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
            healthChecksBuilder.AddHangfire(s => s.MinimumAvailableServers = 1, "hangfire",
                tags: [HealthCheckTag.Readiness.Value]);
        }

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
