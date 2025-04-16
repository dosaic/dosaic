using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddDbMigratorService<TDbContext>(this IServiceCollection serviceCollection)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService<DbMigratorService<TDbContext>>();
        }
        public static void MigrateEfContexts<TDbContext>(this IApplicationBuilder applicationBuilder)
            where TDbContext : DbContext
        {
            var logger = applicationBuilder.ApplicationServices.GetRequiredService<ILogger<EfCorePlugin>>();
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
