using Dosaic.Hosting.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public static class ServiceCollectionExtensions
    {
        public static IHealthChecksBuilder AddEfContext<TContext>(this IHealthChecksBuilder healthChecksBuilder)
            where TContext : DbContext
        {
            healthChecksBuilder.AddDbContextCheck<TContext>(typeof(TContext).Name,
                tags: [HealthCheckTag.Readiness.Value]);
            return healthChecksBuilder;
        }

        public static void MigrateEfContexts<TDbContext>(this IApplicationBuilder applicationBuilder)
            where TDbContext : DbContext
        {
            var logger = applicationBuilder.ApplicationServices.GetRequiredService<ILogger<EntityFrameworkPlugin>>();
            applicationBuilder.ApplicationServices.GetServices<TDbContext>().ToList()
                .ForEach(dbContext =>
                {
                    var dbContextName = dbContext.GetType().Name;
                    logger.LogDebug("Migrating '{DbContextName}'", dbContextName);
                    dbContext.Database.Migrate();
                    logger.LogDebug("Migrated '{DbContextName}'", dbContextName);
                });
        }
    }
}
