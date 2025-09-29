using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeinLinq;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    public static class ServiceCollectionExtensions
    {
        public static void AddNpgsqlDbMigratorService<TDbContext>(this IServiceCollection serviceCollection, bool migrateAllAtOnce = true)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService(sp =>
            {
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                var logger = sp.GetRequiredService<ILogger<NpgsqlDbMigratorService<TDbContext>>>();
                return new NpgsqlDbMigratorService<TDbContext>(scopeFactory, logger, migrateAllAtOnce);
            });
        }

        public static void ConfigureNpgSqlContext<TDbContext>(this DbContextOptionsBuilder builder,
            IServiceProvider provider,
            EfCoreNpgSqlConfiguration configuration,
            Action<NpgSqlConfiguration> configure = null) where TDbContext : DbContext
        {
            var npgSqlConfiguration = new NpgSqlConfiguration();
            configure?.Invoke(npgSqlConfiguration);

            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = configuration.Host,
                Port = configuration.Port,
                Username = configuration.Username,
                Password = configuration.Password,
                Database = configuration.Database,
                ConnectionLifetime = configuration.ConnectionLifetime,
                KeepAlive = configuration.KeepAlive,
                MaxPoolSize = configuration.MaxPoolSize,
                ArrayNullabilityMode = ArrayNullabilityMode.PerInstance,
                IncludeErrorDetail = configuration.IncludeErrorDetail,
            };

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString)
                .MapDbEnums<TDbContext>();
            npgSqlConfiguration.ConfigureDataSource?.Invoke(dataSourceBuilder);
            var dataSource = dataSourceBuilder.Build();

            builder
                .UseNpgsql(dataSource,
                    o =>
                    {
                        o.UseDbEnums<TDbContext>();
                        o.UseQuerySplittingBehavior(configuration.SplitQuery
                            ? QuerySplittingBehavior.SplitQuery
                            : QuerySplittingBehavior.SingleQuery);
                        npgSqlConfiguration.ConfigureNpgSqlContext?.Invoke(o);
                    });

            if (npgSqlConfiguration.CompiledModel is not null)
            {
                builder
                    .UseModel(npgSqlConfiguration.CompiledModel);
            }

            builder.WithLambdaInjection()
                .UseLoggerFactory(loggerFactory)
                .ConfigureLoggingCacheTime(TimeSpan.FromSeconds(configuration.ConfigureLoggingCacheTimeInSeconds));

            if (npgSqlConfiguration.ConfigureWarnings is not null)
                builder
                    .ConfigureWarnings(npgSqlConfiguration.ConfigureWarnings);

            if (configuration.EnableDetailedErrors)
            {
                builder.EnableDetailedErrors();
            }

            if (configuration.EnableSensitiveDataLogging)
            {
                builder.EnableSensitiveDataLogging();
            }
        }
    }

    public class NpgSqlConfiguration
    {
        internal IModel CompiledModel { get; private set; }
        internal Action<NpgsqlDataSourceBuilder> ConfigureDataSource { get; private set; }
        internal Action<NpgsqlDbContextOptionsBuilder> ConfigureNpgSqlContext { get; private set; }
        internal Action<WarningsConfigurationBuilder> ConfigureWarnings { get; private set; }

        public NpgSqlConfiguration WithModel(IModel model)
        {
            CompiledModel = model;
            return this;
        }
        public NpgSqlConfiguration WithDataSource(Action<NpgsqlDataSourceBuilder> configureDataSource)
        {
            ConfigureDataSource = configureDataSource;
            return this;
        }

        public NpgSqlConfiguration WithNpgSql(Action<NpgsqlDbContextOptionsBuilder> configureNpgSql)
        {
            ConfigureNpgSqlContext = configureNpgSql;
            return this;
        }
        public NpgSqlConfiguration WithWarnings(Action<WarningsConfigurationBuilder> configureWarnings)
        {
            ConfigureWarnings = configureWarnings;
            return this;
        }
    }
}
