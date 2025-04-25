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
            Microsoft.EntityFrameworkCore.Metadata.IModel compiledModel = null) where TDbContext : DbContext
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<TDbContext>();
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
                ArrayNullabilityMode = ArrayNullabilityMode.PerInstance
#if DEBUG
                ,
                IncludeErrorDetail = configuration.IncludeErrorDetail
#endif
            };
            builder
                .UseNpgsql(new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString)
                        .MapDbEnums().Build(),
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                        .UseDbEnums())
#if DEBUG
                .EnableSensitiveDataLogging();
#endif
            if (compiledModel != null)
            {
                builder
                    .UseModel(compiledModel);
            }

            builder.UseProjectables()
                .UseLoggerFactory(loggerFactory).ConfigureLoggingCacheTime(TimeSpan.FromMinutes(5))
                .ConfigureWarnings(x => x.Log((CoreEventId.RowLimitingOperationWithoutOrderByWarning, LogLevel.Debug)));
#if DEBUG
            builder.EnableDetailedErrors().EnableSensitiveDataLogging();
            builder.LogTo(s => logger.LogDebug(s));
#endif
        }
    }
}
