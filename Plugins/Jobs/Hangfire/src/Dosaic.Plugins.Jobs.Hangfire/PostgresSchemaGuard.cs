using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace Dosaic.Plugins.Jobs.Hangfire
{
    /// <summary>
    ///     Guards the two assumptions the bulk dispatcher and the prefetching fetch make about the storage:
    ///     that the configured schema name is a plain identifier, and that the schema Hangfire.PostgreSql
    ///     created is one we know how to write to directly.
    /// </summary>
    internal static class PostgresSchemaGuard
    {
        /// <summary>
        ///     Highest <c>Install.v*.sql</c> shipped by Hangfire.PostgreSql 1.21.1. Both the bulk dispatcher
        ///     and the queue client write its private tables directly, so a newer schema has to be reviewed
        ///     before it is used instead of silently corrupting job state.
        /// </summary>
        internal const int SupportedSchemaVersion = 23;

        private static readonly Regex _identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>Rejects schema names that would break out of a quoted identifier.</summary>
        internal static string ValidateName(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema) || !_identifier.IsMatch(schema))
                throw new ArgumentException(
                    $"Invalid Hangfire schema name '{schema}'. Expected a plain identifier matching {_identifier}.",
                    nameof(schema));
            return schema;
        }

        /// <summary>
        ///     Fails fast when the storage migrated past the schema version this plugin was written against.
        /// </summary>
        internal static void AssertSupportedVersion(Func<NpgsqlConnection> connectionFactory, string schema)
        {
            using var connection = connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(
                $"""SELECT max("version"::integer) FROM "{ValidateName(schema)}"."schema";""", connection);
            var value = command.ExecuteScalar();
            if (value is null or DBNull)
                throw new InvalidOperationException(
                    $"Hangfire schema '{schema}' has no version row - the schema was not prepared.");
            var version = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (version > SupportedSchemaVersion)
                throw new InvalidOperationException(
                    $"Hangfire schema '{schema}' is at version {version}, but batching and prefetching were " +
                    $"written against version {SupportedSchemaVersion}. Review the migration before enabling them.");
        }
    }
}
