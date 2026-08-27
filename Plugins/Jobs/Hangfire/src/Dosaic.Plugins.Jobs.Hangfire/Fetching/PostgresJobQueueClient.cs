using Npgsql;
using NpgsqlTypes;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    internal sealed class PostgresJobQueueClient : IJobQueueClient
    {
        private readonly Func<NpgsqlConnection> _connectionFactory;
        private readonly string _fetchSql;
        private readonly string _removeSql;
        private readonly string _requeueSql;
        private readonly string _keepAliveSql;
        private readonly string _keepAliveManySql;

        public PostgresJobQueueClient(Func<NpgsqlConnection> connectionFactory, string schema)
        {
            _connectionFactory = connectionFactory;
            var validated = PostgresSchemaGuard.ValidateName(schema);
            _fetchSql = FetchSql(validated);
            _removeSql = RemoveSql(validated);
            _requeueSql = RequeueSql(validated);
            _keepAliveSql = KeepAliveSql(validated);
            _keepAliveManySql = KeepAliveManySql(validated);
        }

        public IReadOnlyList<PrefetchedQueueEntry> Fetch(string[] queues, int count, TimeSpan invisibilityTimeout)
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(_fetchSql, connection);
            command.Parameters.Add(new NpgsqlParameter("queues", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = queues });
            command.Parameters.Add(new NpgsqlParameter("timeout", NpgsqlDbType.Interval) { Value = invisibilityTimeout });
            command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = count });
            using var reader = command.ExecuteReader();
            var entries = new List<PrefetchedQueueEntry>(count);
            while (reader.Read())
                entries.Add(new PrefetchedQueueEntry(reader.GetInt64(0), reader.GetInt64(1), reader.GetDateTime(2)));
            return entries;
        }

        public void Remove(long queueEntryId, DateTime fetchedAt) =>
            Execute(_removeSql, queueEntryId, fetchedAt);

        public void Requeue(long queueEntryId, DateTime fetchedAt) =>
            Execute(_requeueSql, queueEntryId, fetchedAt);

        public DateTime? KeepAlive(long queueEntryId, DateTime fetchedAt)
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = Command(_keepAliveSql, connection, queueEntryId, fetchedAt);
            return command.ExecuteScalar() is DateTime renewed ? renewed : null;
        }

        public IReadOnlyDictionary<long, DateTime> KeepAlive(IReadOnlyList<PrefetchedQueueEntry> entries)
        {
            var renewed = new Dictionary<long, DateTime>(entries.Count);
            if (entries.Count == 0) return renewed;
            using var connection = _connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(_keepAliveManySql, connection);
            command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
            {
                Value = entries.Select(x => x.QueueEntryId).ToArray()
            });
            command.Parameters.Add(new NpgsqlParameter("fetchedats", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            {
                Value = entries.Select(x => x.FetchedAt.ToUniversalTime()).ToArray()
            });
            using var reader = command.ExecuteReader();
            while (reader.Read()) renewed[reader.GetInt64(0)] = reader.GetDateTime(1);
            return renewed;
        }

        private void Execute(string sql, long queueEntryId, DateTime fetchedAt)
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = Command(sql, connection, queueEntryId, fetchedAt);
            command.ExecuteNonQuery();
        }

        private static NpgsqlCommand Command(string sql, NpgsqlConnection connection, long queueEntryId,
            DateTime fetchedAt)
        {
            var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = queueEntryId });
            command.Parameters.Add(new NpgsqlParameter("fetchedat", NpgsqlDbType.TimestampTz)
            {
                Value = fetchedAt.ToUniversalTime()
            });
            return command;
        }

        internal static string RemoveSql(string schema) =>
            $"""DELETE FROM "{schema}"."jobqueue" WHERE "id" = @id AND "fetchedat" = @fetchedat;""";

        internal static string RequeueSql(string schema) =>
            $"""UPDATE "{schema}"."jobqueue" SET "fetchedat" = NULL WHERE "id" = @id AND "fetchedat" = @fetchedat;""";

        internal static string KeepAliveSql(string schema) =>
            $"""
             UPDATE "{schema}"."jobqueue" SET "fetchedat" = NOW()
             WHERE "id" = @id AND "fetchedat" = @fetchedat
             RETURNING "fetchedat";
             """;

        internal static string KeepAliveManySql(string schema) =>
            $"""
             UPDATE "{schema}"."jobqueue" AS q SET "fetchedat" = NOW()
             FROM unnest(@ids::bigint[], @fetchedats::timestamptz[]) AS i("id", "fetchedat")
             WHERE q."id" = i."id" AND q."fetchedat" = i."fetchedat"
             RETURNING q."id", q."fetchedat";
             """;

        internal static string FetchSql(string schema) => $"""
            UPDATE "{schema}"."jobqueue"
            SET "fetchedat" = NOW()
            WHERE "id" IN (
                SELECT "id"
                FROM "{schema}"."jobqueue"
                WHERE "queue" = ANY (@queues)
                  AND ("fetchedat" IS NULL OR "fetchedat" < NOW() - @timeout)
                ORDER BY "fetchedat" NULLS FIRST, "queue", "jobid"
                FOR UPDATE SKIP LOCKED
                LIMIT @limit
            )
            RETURNING "id", "jobid", "fetchedat";
            """;
    }
}
