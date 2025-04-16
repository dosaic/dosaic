using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.EfCore.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using EntityFrameworkCore.Projectables.Infrastructure;
using EntityFrameworkCore.Projectables.Infrastructure.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    public class EfCoreNpgSqlPlugin(
        IImplementationResolver implementationResolver,
        EfCoreNpgSqlConfiguration configuration) : IPluginServiceConfiguration,
        IPluginApplicationConfiguration, IPluginHealthChecksConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
        }


        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
        }

        private void ConfigureDatabase<TDbContext>(IServiceProvider provider, DbContextOptionsBuilder builder,
            Microsoft.EntityFrameworkCore.Metadata.IModel compiledModel = null)
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
                ConnectionLifetime = 60,
                KeepAlive = 15,
                MaxPoolSize = 100,
                ArrayNullabilityMode = ArrayNullabilityMode.PerInstance
#if DEBUG
                ,
                IncludeErrorDetail = true
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

    public static class DbContextOptionsExtensions
    {
        /// <summary>
        /// Use projectables within the queries. Any call to a Projectable property/method will automatically be translated to the underlying expression tree instead
        /// </summary>
        public static DbContextOptionsBuilder<TContext> UseProjectables<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder, Action<ProjectableOptionsBuilder> configure = null)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseProjectables((DbContextOptionsBuilder)optionsBuilder, configure);

        /// <summary>
        /// Use projectables within the queries. Any call to a Projectable property/method will automatically be translated to the underlying expression tree instead
        /// </summary>
        public static DbContextOptionsBuilder UseProjectables(this DbContextOptionsBuilder optionsBuilder,
            Action<ProjectableOptionsBuilder> configure = null)
        {
#pragma warning disable EF1001
            var extension = optionsBuilder.Options.FindExtension<ProjectionOptionsExtension>() ??
                            new ProjectionOptionsExtension();
#pragma warning restore EF1001
#pragma warning disable EF1001
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
#pragma warning restore EF1001

            configure?.Invoke(new ProjectableOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }
    }
}
