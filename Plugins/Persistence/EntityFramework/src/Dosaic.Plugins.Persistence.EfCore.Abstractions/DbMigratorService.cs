using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.EntityFramework
{
    [ExcludeFromCodeCoverage(Justification = "Needs an actual database")]
    internal class DbMigratorService<TContext>(IServiceScopeFactory scopeFactory, ILogger logger)
        : BackgroundService where TContext : DbContext
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TContext>();
                    if (!db.Database.IsRelational()) return;
                    await db.Database.MigrateAsync(stoppingToken);
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Could not migrate database");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
