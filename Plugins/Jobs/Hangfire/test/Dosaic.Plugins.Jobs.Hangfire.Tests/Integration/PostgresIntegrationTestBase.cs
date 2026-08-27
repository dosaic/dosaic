using System.Globalization;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Newtonsoft.Json;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Integration
{
    /// <summary>
    ///     Spins up a throwaway PostgreSQL and lets Hangfire create its real schema in it.
    ///     Marked <see cref="ExplicitAttribute" /> so a plain <c>dotnet test</c> never needs Docker — run these with
    ///     <c>dotnet test --filter TestCategory=Integration</c>.
    /// </summary>
    [Explicit("Requires Docker. Run with: dotnet test --filter TestCategory=Integration")]
    [Category("Integration")]
    [NonParallelizable]
    public abstract class PostgresIntegrationTestBase
    {
        protected const string SchemaName = "hangfire";
        protected const string DatabaseName = "hangfire_tests";
        protected const string DatabaseUser = "hangfire";
        protected const string DatabasePassword = "hangfire";
        private PostgreSqlContainer _postgres;
        protected string DatabaseHost => _postgres.Hostname;
        protected int DatabasePort => _postgres.GetMappedPublicPort(5432);
        protected string ConnectionString { get; private set; }
        protected PostgreSqlStorage Storage { get; private set; }

        [OneTimeSetUp]
        public async Task StartDatabaseAsync()
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase(DatabaseName)
                .WithUsername(DatabaseUser)
                .WithPassword(DatabasePassword)
                // pg_stat_statements is what CountExecutedStatementsAsync counts with, and it has to be
                // preloaded at server start - the extension cannot be added to a running server
                .WithCommand("-c", "shared_preload_libraries=pg_stat_statements")
                .Build();
            await _postgres.StartAsync();
            ConnectionString = _postgres.GetConnectionString();
            await ExecuteAsync("CREATE EXTENSION IF NOT EXISTS pg_stat_statements;");

            var storageOptions = new PostgreSqlStorageOptions
            {
                SchemaName = SchemaName,
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromMilliseconds(200)
            };
            Storage = new PostgreSqlStorage(new NpgsqlConnectionFactory(ConnectionString, storageOptions),
                storageOptions);
            GlobalConfiguration.Configuration
                .UseSerializerSettings(new JsonSerializerSettings())
                .UseSimpleAssemblyNameTypeSerializer()
                // another fixture may have left a DI backed activator behind that points at a disposed provider
                .UseActivator(new JobActivator())
                .UseStorage(Storage);
            // other fixtures in this assembly register this filter with a mocked feature manager
            foreach (var filter in GlobalJobFilters.Filters
                         .Where(x => x.Instance is EnabledByFeatureFilter).ToList())
                GlobalJobFilters.Filters.Remove(filter.Instance);
        }

        [OneTimeTearDown]
        public async Task StopDatabaseAsync()
        {
            (Storage as IDisposable)?.Dispose();
            if (_postgres is not null) await _postgres.DisposeAsync();
        }

        protected NpgsqlConnection CreateConnection() => new(ConnectionString);

        protected async Task ExecuteAsync(string sql)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        protected async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T), CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Forgets everything PostgreSQL counted so far, so that
        ///     <see cref="CountExecutedStatementsAsync" /> only counts what happens afterwards.
        /// </summary>
        protected Task ResetStatementCountsAsync() => ExecuteAsync("SELECT pg_stat_statements_reset();");

        /// <summary>
        ///     Counts how often the database was actually asked to run a statement matching
        ///     <paramref name="statementPattern" />, by asking PostgreSQL's own statement statistics.
        ///     Exact and immediate - the counter is updated when the statement finishes.
        /// </summary>
        protected async Task<int> CountExecutedStatementsAsync(string statementPattern)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """SELECT COALESCE(SUM("calls"), 0) FROM pg_stat_statements WHERE "query" ~ @pattern;""",
                connection);
            command.Parameters.Add(new NpgsqlParameter("pattern", statementPattern));
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        protected static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string because)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(100);
            }

            Assert.Fail($"Timed out after {timeout} waiting until {because}.");
        }
    }
}
