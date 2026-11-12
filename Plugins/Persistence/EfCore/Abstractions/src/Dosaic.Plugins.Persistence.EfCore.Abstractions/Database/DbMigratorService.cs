using System.Diagnostics.CodeAnalysis;
using Dosaic.Hosting.Abstractions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    [ExcludeFromCodeCoverage(Justification = "Needs an actual database")]
    internal class DbMigratorService<TContext>(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        StartupRetryOptions retryOptions = null,
        IHostApplicationLifetime lifetime = null)
        : BackgroundService where TContext : DbContext
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            StartupRetry.RunAsync(retryOptions, "migrate database", MigrateAsync, _ => TimeSpan.FromSeconds(1),
                logger, lifetime, stoppingToken);

        private async Task MigrateAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            if (!db.Database.IsRelational()) return;
            await db.Database.MigrateAsync(stoppingToken);
        }
    }
}
