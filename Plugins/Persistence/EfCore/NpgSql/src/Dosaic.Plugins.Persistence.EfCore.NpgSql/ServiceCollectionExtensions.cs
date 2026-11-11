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
        ///     <see cref="EfCoreNpgSqlConfiguration.ConnectionParameters" /> first, then every discrete setting on top.
        ///     The <c>WithDataSource</c> hook of the consumer runs afterwards and therefore still wins.
        /// </summary>
        internal static NpgsqlConnectionStringBuilder CreateConnectionStringBuilder(
            EfCoreNpgSqlConfiguration configuration)
        {
            NpgsqlConnectionStringBuilder builder;
            try
            {
                builder = new NpgsqlConnectionStringBuilder(configuration.ConnectionParameters ?? "");
            }
            catch (Exception e)
            {
                // the raw value may carry credentials, so neither it nor the inner message is repeated here
                throw new InvalidOperationException(
                    $"Configuration: npgsql:connectionParameters is not a valid Npgsql connection string ({e.GetType().Name}).");
            }

            if (!string.IsNullOrEmpty(configuration.Host)) builder.Host = configuration.Host;
            builder.Port = configuration.Port;
            if (!string.IsNullOrEmpty(configuration.Username)) builder.Username = configuration.Username;
            if (!string.IsNullOrEmpty(configuration.Password)) builder.Password = configuration.Password;
            if (!string.IsNullOrEmpty(configuration.Database)) builder.Database = configuration.Database;
            builder.ConnectionLifetime = configuration.ConnectionLifetime;
            builder.KeepAlive = configuration.KeepAlive;
            builder.MaxPoolSize = configuration.MaxPoolSize;
            builder.ArrayNullabilityMode = ArrayNullabilityMode.PerInstance;
            builder.IncludeErrorDetail = configuration.IncludeErrorDetail;
            if (!string.IsNullOrWhiteSpace(configuration.SslMode)) builder.SslMode = ParseSslMode(configuration.SslMode);
            if (!string.IsNullOrWhiteSpace(configuration.RootCertificate))
                builder.RootCertificate = configuration.RootCertificate;
            if (configuration.NoResetOnClose) builder.NoResetOnClose = true;
            return builder;
        }

        private static SslMode ParseSslMode(string value)
        {
            var name = value.Trim();
            // Enum.TryParse also accepts numbers, which would silently pick a mode nobody wrote down
            if (name.All(char.IsLetter) && Enum.TryParse<SslMode>(name, true, out var sslMode))
                return sslMode;
            throw new InvalidOperationException(
                $"Configuration: npgsql:sslMode '{value}' is not a valid Npgsql SslMode. " +
                $"Expected one of: {string.Join(", ", Enum.GetNames<SslMode>())}.");
        }

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
