using System.Data;
using System.Diagnostics.CodeAnalysis;
using Dosaic.Hosting.Abstractions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    [ExcludeFromCodeCoverage(Justification = "Needs an actual postgres database")]
    public class NpgsqlDbMigratorService<TDbContext> : BackgroundService where TDbContext : DbContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        private readonly StartupRetryOptions _retryOptions;
        private readonly IHostApplicationLifetime _lifetime;

        /// <summary>Retries until the host stops.</summary>
        public NpgsqlDbMigratorService(IServiceScopeFactory scopeFactory, ILogger logger)
            : this(scopeFactory, logger, null, null)
        {
        }

        /// <summary>
        ///     Retries as <see cref="EfCoreNpgSqlConfiguration.Migration" /> (<c>npgsql:migration</c>) says. The
        ///     dependency injection picks this constructor when the <c>npgsql</c> configuration is registered,
        ///     which the Dosaic web host does.
        /// </summary>
        public NpgsqlDbMigratorService(IServiceScopeFactory scopeFactory, ILogger logger,
            EfCoreNpgSqlConfiguration configuration, IHostApplicationLifetime lifetime)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _retryOptions = configuration?.Migration;
            _lifetime = lifetime;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            StartupRetry.RunAsync(_retryOptions, "migrate database", MigrateAsync, _ => TimeSpan.FromSeconds(1),
                _logger, _lifetime, stoppingToken);

        private async Task MigrateAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            if (!db.Database.IsRelational()) return;
            var migrations = await db.Database.GetPendingMigrationsAsync(stoppingToken);
            foreach (var migration in migrations.Order())
            {
                _logger.LogInformation("Applying migration {Migration}", migration);
                await db.Database.MigrateAsync(migration, stoppingToken);
            }

            if (db.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection) return;
            await ReloadDbTypesAsync(npgsqlConnection, stoppingToken);
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
