using Npgsql;
using NpgsqlTypes;

namespace Dosaic.Plugins.Jobs.Hangfire.Uniqueness
{
    /// <summary>
    ///     Claims fingerprints with a single <c>INSERT ... ON CONFLICT</c>. The unique index on
    ///     <c>set(key, value)</c> arbitrates, so two concurrent clients can never both win, and an expired
    ///     claim is taken over by the same statement.
    /// </summary>
    internal sealed class PostgresJobUniquenessStore : IJobUniquenessStore
    {
        private readonly Func<NpgsqlConnection> _connectionFactory;
        private readonly string _schema;

        public PostgresJobUniquenessStore(Func<NpgsqlConnection> connectionFactory, string schema)
        {
            _connectionFactory = connectionFactory;
            _schema = schema;
        }

        public IReadOnlyCollection<JobUniquenessClaim> Claim(IReadOnlyList<JobUniquenessClaim> claims, double now)
        {
            if (claims.Count == 0) return [];
            var byIdentity = new Dictionary<(string, string), JobUniquenessClaim>(claims.Count);
            foreach (var claim in claims) byIdentity[(claim.SetKey, claim.Fingerprint)] = claim;
            var distinct = byIdentity.Values.ToList();

            using var connection = _connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(BuildSql(_schema), connection);
            Add(command, "key", NpgsqlDbType.Array | NpgsqlDbType.Text, distinct.Select(x => x.SetKey).ToArray());
            Add(command, "value", NpgsqlDbType.Array | NpgsqlDbType.Text, distinct.Select(x => x.Fingerprint).ToArray());
            Add(command, "score", NpgsqlDbType.Array | NpgsqlDbType.Double, distinct.Select(x => x.ExpiresAt).ToArray());
            Add(command, "now", NpgsqlDbType.Double, now);

            var owned = new List<JobUniquenessClaim>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                owned.Add(byIdentity[(reader.GetString(0), reader.GetString(1))]);
            return owned;
        }

        private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object value) =>
            command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });

        internal static string BuildSql(string schema) => $$"""
            INSERT INTO "{{schema}}"."set" ("key", "value", "score", "expireat")
            SELECT t."key", t."value", t."score", to_timestamp(t."score")
            FROM unnest(@key::text[], @value::text[], @score::float8[]) AS t("key", "value", "score")
            ON CONFLICT ("key", "value") DO UPDATE
                SET "score" = EXCLUDED."score", "expireat" = EXCLUDED."expireat"
                WHERE "set"."score" <= @now
            RETURNING "key", "value";
            """;
    }
}
