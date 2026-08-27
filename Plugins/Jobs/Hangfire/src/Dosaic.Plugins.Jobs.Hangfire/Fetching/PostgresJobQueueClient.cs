using Npgsql;
using NpgsqlTypes;

namespace Dosaic.Plugins.Jobs.Hangfire.Fetching
{
    internal sealed class PostgresJobQueueClient : IJobQueueClient
    {
        private readonly Func<NpgsqlConnection> _connectionFactory;
        private readonly string _schema;

        public PostgresJobQueueClient(Func<NpgsqlConnection> connectionFactory, string schema)
        {
            _connectionFactory = connectionFactory;
            _schema = schema;
        }

        public IReadOnlyList<PrefetchedQueueEntry> Fetch(string[] queues, int count, TimeSpan invisibilityTimeout)
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(FetchSql(_schema), connection);
            command.Parameters.Add(new NpgsqlParameter("queues", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = queues });
            command.Parameters.Add(new NpgsqlParameter("timeout", NpgsqlDbType.Interval) { Value = invisibilityTimeout });
            command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = count });
            using var reader = command.ExecuteReader();
            var entries = new List<PrefetchedQueueEntry>(count);
            while (reader.Read())
                entries.Add(new PrefetchedQueueEntry(reader.GetInt64(0), reader.GetInt64(1)));
            return entries;
        }

        public void Remove(long queueEntryId) =>
            Execute($"""DELETE FROM "{_schema}"."jobqueue" WHERE "id" = @id;""", queueEntryId);

        public void Requeue(long queueEntryId) =>
            Execute($"""UPDATE "{_schema}"."jobqueue" SET "fetchedat" = NULL WHERE "id" = @id;""", queueEntryId);

        public void KeepAlive(long queueEntryId) =>
            Execute($"""UPDATE "{_schema}"."jobqueue" SET "fetchedat" = NOW() WHERE "id" = @id;""", queueEntryId);

        private void Execute(string sql, long queueEntryId)
        {
            using var connection = _connectionFactory();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = queueEntryId });
            command.ExecuteNonQuery();
        }

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
            RETURNING "id", "jobid";
            """;
    }
}
