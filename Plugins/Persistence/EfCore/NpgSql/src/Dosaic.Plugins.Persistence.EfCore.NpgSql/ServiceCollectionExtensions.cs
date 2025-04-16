using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    public static class ServiceCollectionExtensions
    {
        public static void AddNpgsqlDbMigratorService<TDbContext>(this IServiceCollection serviceCollection)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService<NpgsqlDbMigratorService<TDbContext>>();
        }
    }
}
