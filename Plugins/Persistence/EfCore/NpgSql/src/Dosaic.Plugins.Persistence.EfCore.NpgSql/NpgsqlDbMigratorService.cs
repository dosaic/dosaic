using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    [ExcludeFromCodeCoverage(Justification = "Needs an actual postgres database")]
    public class NpgsqlDbMigratorService<TDbContext>(IServiceScopeFactory scopeFactory, ILogger logger)
        : BackgroundService where TDbContext : DbContext
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                    if (!db.Database.IsRelational()) return;
                    await db.Database.MigrateAsync(stoppingToken);
                    if (db.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection) return;
                    await ReloadDbTypesAsync(npgsqlConnection, stoppingToken);
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Could not migrate database");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private static async Task ReloadDbTypesAsync(NpgsqlConnection npgsqlConnection, CancellationToken stoppingToken)
        {
            if (npgsqlConnection.State != ConnectionState.Open)
                await npgsqlConnection.OpenAsync(stoppingToken);
            try
            {
                await npgsqlConnection.ReloadTypesAsync(stoppingToken);
            }
            finally
            {
                await npgsqlConnection.CloseAsync();
            }
        }
    }
}
