using Dosaic.Hosting.Abstractions.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
{
    /// <summary>
    ///     Builds the connection string from the <c>npgsql</c> section once at startup, so a missing section, a
    ///     missing host, database or username, an invalid <c>sslMode</c> or malformed <c>connectionParameters</c>
    ///     stop the host instead of failing the first DbContext.
    /// </summary>
    public class EfCoreNpgSqlPlugin(EfCoreNpgSqlConfiguration configuration) : IPluginServiceConfiguration
    {
        private readonly EfCoreNpgSqlConfiguration _configuration = configuration ?? new EfCoreNpgSqlConfiguration();

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            var builder = ServiceCollectionExtensions.CreateConnectionStringBuilder(_configuration);
            EnsureRequiredKeys(builder);
        }

        private static readonly (string Key, string Keyword, Func<NpgsqlConnectionStringBuilder, string> Value)[]
            RequiredKeys =
            [
                ("npgsql:host", "Host", b => b.Host),
                ("npgsql:database", "Database", b => b.Database),
                ("npgsql:username", "Username", b => b.Username)
            ];

        private void EnsureRequiredKeys(NpgsqlConnectionStringBuilder builder)
        {
            var missing = RequiredKeys.Where(x => string.IsNullOrWhiteSpace(x.Value(builder))).ToList();
            if (missing.Count == 0) return;
            var keys = string.Join(", ", missing.Select(x => x.Key));
            var keywords = string.Join(", ", missing.Select(x => x.Keyword));
            var sectionIsEmpty = missing.Count == RequiredKeys.Length &&
                                 string.IsNullOrWhiteSpace(_configuration.ConnectionParameters) &&
                                 string.IsNullOrEmpty(_configuration.Password);
            var problem = sectionIsEmpty
                ? "Configuration section 'npgsql' is missing or empty."
                : "Configuration section 'npgsql' is incomplete.";
            throw new InvalidOperationException(
                $"{problem} Set {keys} (environment: {string.Join(", ", missing.Select(x => ToEnvironmentName(x.Key)))}) or {keywords} in npgsql:connectionParameters.");
        }

        private static string ToEnvironmentName(string key) => key.Replace(':', '_').ToUpperInvariant();
    }
}
