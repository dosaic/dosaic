using Dosaic.Hosting.Abstractions.Configuration;
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
        public static void AddNpgsqlDbMigratorService<TDbContext>(this IServiceCollection serviceCollection)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService<NpgsqlDbMigratorService<TDbContext>>();
        }

        /// <summary>
        ///     <see cref="EfCoreNpgSqlConfiguration.ConnectionParameters" /> first, then every configured discrete
        ///     setting on top. Defaults only fill in what the parameters leave unset, the same rules
        ///     <see cref="ConnectionStringSettings" /> applies for every plugin. The <c>WithDataSource</c> hook of
        ///     the consumer runs afterwards and therefore still wins.
        /// </summary>
        internal static NpgsqlConnectionStringBuilder CreateConnectionStringBuilder(
            EfCoreNpgSqlConfiguration configuration) =>
            ConnectionStringSettings.Parse<NpgsqlConnectionStringBuilder>("npgsql:connectionParameters",
                    configuration.ConnectionParameters)
                .Override(!string.IsNullOrEmpty(configuration.Host), b => b.Host = configuration.Host)
                .OverrideOrDefault(configuration.PortConfigured, b => b.Port = configuration.Port)
                .Override(!string.IsNullOrEmpty(configuration.Username), b => b.Username = configuration.Username)
                .Override(!string.IsNullOrEmpty(configuration.Password), b => b.Password = configuration.Password)
                .Override(!string.IsNullOrEmpty(configuration.Database), b => b.Database = configuration.Database)
                .OverrideOrDefault(configuration.ConnectionLifetimeConfigured,
                    b => b.ConnectionLifetime = configuration.ConnectionLifetime)
                .OverrideOrDefault(configuration.KeepAliveConfigured, b => b.KeepAlive = configuration.KeepAlive)
                .OverrideOrDefault(configuration.MaxPoolSizeConfigured,
                    b => b.MaxPoolSize = configuration.MaxPoolSize)
                // the plugin maps arrays with this mode, the parameters must not change it
                .Override(true, b => b.ArrayNullabilityMode = ArrayNullabilityMode.PerInstance)
                .OverrideOrDefault(configuration.IncludeErrorDetailConfigured,
                    b => b.IncludeErrorDetail = configuration.IncludeErrorDetail)
                .Override(!string.IsNullOrWhiteSpace(configuration.SslMode),
                    b => b.SslMode = ConnectionStringSettings.ParseEnumName<SslMode>("npgsql:sslMode",
                        configuration.SslMode))
                .Override(!string.IsNullOrWhiteSpace(configuration.RootCertificate),
                    b => b.RootCertificate = configuration.RootCertificate)
                .Override(configuration.NoResetOnClose, b => b.NoResetOnClose = true)
                .Builder;

        public static void ConfigureNpgSqlContext<TDbContext>(this DbContextOptionsBuilder builder,
            IServiceProvider provider,
            EfCoreNpgSqlConfiguration configuration,
            Action<NpgSqlConfiguration> configure = null) where TDbContext : DbContext
        {
            var npgSqlConfiguration = new NpgSqlConfiguration();
            configure?.Invoke(npgSqlConfiguration);

            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var connectionStringBuilder = CreateConnectionStringBuilder(configuration);

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
