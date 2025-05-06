using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    public static class ServiceCollectionExtensions
    {
        public static void AddNpgsqlDbMigratorService<TDbContext>(this IServiceCollection serviceCollection)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService<NpgsqlDbMigratorService<TDbContext>>();
        }

        public static void ConfigureNpgSqlDatabase<TDbContext>(IServiceProvider provider,
            DbContextOptionsBuilder builder, EfCoreNpgSqlConfiguration configuration,
            Action<WarningsConfigurationBuilder> warningsConfigurationBuilderAction = null,
            Microsoft.EntityFrameworkCore.Metadata.IModel compiledModel = null) where TDbContext : DbContext
        {
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

            builder
                .UseNpgsql(new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString)
                        .MapDbEnums<TDbContext>().Build(),
                    o => o.UseQuerySplittingBehavior(configuration.SplitQuery
                            ? QuerySplittingBehavior.SplitQuery
                            : QuerySplittingBehavior.SingleQuery)
                        .UseDbEnums<TDbContext>());

            if (compiledModel != null)
            {
                builder
                    .UseModel(compiledModel);
            }

            builder.UseProjectables()
                .UseLoggerFactory(loggerFactory)
                .ConfigureLoggingCacheTime(TimeSpan.FromSeconds(configuration.ConfigureLoggingCacheTimeInSeconds));
            if (warningsConfigurationBuilderAction != null)
                builder
                    .ConfigureWarnings(warningsConfigurationBuilderAction);

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
}
