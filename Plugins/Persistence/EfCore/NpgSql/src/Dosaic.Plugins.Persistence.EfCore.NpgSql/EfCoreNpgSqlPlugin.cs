using Dosaic.Hosting.Abstractions.Extensions;
using EntityFrameworkCore.Projectables.Infrastructure;
using EntityFrameworkCore.Projectables.Infrastructure.Internal;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;


namespace Dosaic.Plugins.Persistence.EntityFramework
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
                        .MapDbEnums<TDbContext>().Build(),
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                        .UseDbEnums<TDbContext>())
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

    internal static class PostgresEnumExtensions
    {
        private static readonly NpgsqlSnakeCaseNameTranslator _translator = new();

        private static HashSet<Type> getEnumTypes<T>() =>
            typeof(T).GetAssemblyTypes(x => x.IsEnum && x.HasAttribute<DbEnumAttribute>()).ToHashSet();

        public static void MapDbEnums<TDbContext>(this ModelBuilder builder)
        {
            var register = typeof(NpgsqlModelBuilderExtensions).GetMethods()
                .Single(x =>
                {
                    if (x is not
                        {
                            Name: nameof(NpgsqlModelBuilderExtensions.HasPostgresEnum), ContainsGenericParameters: false
                        })
                        return false;
                    var parameters = x.GetParameters();
                    return parameters.Length == 4
                           && parameters[0].ParameterType == typeof(ModelBuilder)
                           && parameters[1].ParameterType == typeof(string)
                           && parameters[2].ParameterType == typeof(string)
                           && parameters[3].ParameterType == typeof(string[]);
                });
            foreach (var e in getEnumTypes<TDbContext>())
            {
                var dbEnum = e.GetAttribute<DbEnumAttribute>();
                var labels = Enum.GetNames(e).Select(_translator.TranslateMemberName).Order().ToArray();
                register.Invoke(null, [builder, dbEnum.Schema, dbEnum.Name, labels]);
            }
        }

        public static NpgsqlDataSourceBuilder MapDbEnums<TDbContext>(this NpgsqlDataSourceBuilder dataSourceBuilder)
        {
            dataSourceBuilder.EnableUnmappedTypes();
            foreach (var e in getEnumTypes<TDbContext>())
            {
                var dbName = e.GetAttribute<DbEnumAttribute>()!.DbName;
                dataSourceBuilder.MapEnum(e, dbName);
            }

            return dataSourceBuilder;
        }

        public static NpgsqlDbContextOptionsBuilder UseDbEnums<TDbContext>(this NpgsqlDbContextOptionsBuilder builder)
        {
            foreach (var e in getEnumTypes<TDbContext>())
            {
                var dbEnum = e.GetAttribute<DbEnumAttribute>();
                builder.MapEnum(e, dbEnum.Name, dbEnum.Schema, _translator);
            }

            return builder;
        }
    }

    [AttributeUsage(AttributeTargets.Enum)]
    public class DbEnumAttribute(string name, string schema) : Attribute
    {
        public string Name { get; } = name;
        public string Schema { get; } = schema;

        public string DbName => $"{Schema}.{Name}";
    }


    [AttributeUsage(AttributeTargets.Class)]
    public class DbNanoIdPrimaryKeyAttribute(byte length, string prefix = "") : Attribute
    {
        public string Prefix { get; } = prefix;
        public byte Length { get; } = length;
        public byte LengthWithPrefix => (byte)(Length + Prefix.Length);
    }
}
