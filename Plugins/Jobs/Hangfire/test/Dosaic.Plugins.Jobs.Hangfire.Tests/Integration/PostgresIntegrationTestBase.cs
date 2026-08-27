using System.Globalization;
using System.Text.RegularExpressions;
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
                .Build();
            await _postgres.StartAsync();
            ConnectionString = _postgres.GetConnectionString();
            await ExecuteAsync("ALTER SYSTEM SET log_statement = 'all';");
            await ExecuteAsync("SELECT pg_reload_conf();");

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
        ///     Remembers how much of PostgreSQL's statement log has been written so far, so that
        ///     <see cref="CountExecutedStatementsAsync" /> only counts what happens afterwards.
        /// </summary>
        protected async Task<LogMark> MarkLogAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var logs = await _postgres.GetLogsAsync();
            return new LogMark(logs.Stdout.Length, logs.Stderr.Length);
        }

        /// <summary>
        ///     Counts how often the database was actually asked to run a statement matching
        ///     <paramref name="statementPattern" />, by reading PostgreSQL's own statement log.
        /// </summary>
        protected async Task<int> CountExecutedStatementsAsync(LogMark mark, string statementPattern)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var logs = await _postgres.GetLogsAsync();
            var pattern = new Regex($@"(statement|execute [^:]*):\s*{statementPattern}", RegexOptions.IgnoreCase);
            return pattern.Matches(logs.Stdout[Math.Min(mark.Stdout, logs.Stdout.Length)..]).Count
                   + pattern.Matches(logs.Stderr[Math.Min(mark.Stderr, logs.Stderr.Length)..]).Count;
        }

        protected sealed record LogMark(int Stdout, int Stderr);

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
