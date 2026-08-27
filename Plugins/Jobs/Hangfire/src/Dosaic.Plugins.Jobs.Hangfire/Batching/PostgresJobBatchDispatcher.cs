using System.Globalization;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire.Common;
using Hangfire.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    /// <summary>
    ///     Writes a whole batch of jobs — job rows, state rows, queue rows, scheduling sets, job parameters
    ///     and continuation links — with a single statement, and therefore a single database round trip.
    /// </summary>
    internal sealed class PostgresJobBatchDispatcher : IJobBatchDispatcher
    {
        private readonly Func<NpgsqlConnection> _connectionFactory;
        private readonly string _schema;
        private readonly int _chunkSize;

        public PostgresJobBatchDispatcher(Func<NpgsqlConnection> connectionFactory, string schema, int chunkSize)
        {
            _connectionFactory = connectionFactory;
            _schema = schema;
            _chunkSize = chunkSize;
        }

        public async Task<IReadOnlyList<string>> DispatchAsync(IReadOnlyList<BatchJobEntry> entries,
            CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0) return [];
            var ids = new string[entries.Count];
            await using var connection = _connectionFactory();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            foreach (var chunk in BatchChunker.Chunk(entries, _chunkSize))
                await WriteAsync(connection, chunk, ids, cancellationToken).ConfigureAwait(false);
            return ids;
        }

        private async Task WriteAsync(NpgsqlConnection connection, IReadOnlyList<BatchJobEntry> chunk, string[] ids,
            CancellationToken cancellationToken)
        {
            var parameters = BuildParameters(chunk);
            await using var command = new NpgsqlCommand(BuildSql(_schema), connection);
            Add(command, "invocationdata", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.InvocationData);
            Add(command, "arguments", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.Arguments);
            Add(command, "statename", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.StateNames);
            Add(command, "statereason", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.StateReasons);
            Add(command, "statedata", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.StateData);
            Add(command, "queue", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.Queues);
            Add(command, "setkey", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.SetKeys);
            Add(command, "setprefix", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.SetPrefixes);
            Add(command, "setscore", NpgsqlDbType.Array | NpgsqlDbType.Double, parameters.SetScores);
            Add(command, "parentidx", NpgsqlDbType.Array | NpgsqlDbType.Integer, parameters.ParentIndexes);
            Add(command, "continuationoptions", NpgsqlDbType.Array | NpgsqlDbType.Integer, parameters.ContinuationOptions);
            Add(command, "paramidx", NpgsqlDbType.Array | NpgsqlDbType.Integer, parameters.ParameterIndexes);
            Add(command, "paramname", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.ParameterNames);
            Add(command, "paramvalue", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.ParameterValues);
            Add(command, "unikey", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.UniqueSetKeys);
            Add(command, "fingerprint", NpgsqlDbType.Array | NpgsqlDbType.Text, parameters.UniqueFingerprints);
            Add(command, "uniqueexpiry", NpgsqlDbType.Array | NpgsqlDbType.Double, parameters.UniqueExpiresAt);
            Add(command, "presuppressed", NpgsqlDbType.Array | NpgsqlDbType.Boolean, parameters.UniqueDuplicates);
            Add(command, "rootidx", NpgsqlDbType.Array | NpgsqlDbType.Integer, parameters.RootIndexes);
            Add(command, "uniquenow", NpgsqlDbType.Double, JobFingerprint.ToTimestamp(DateTime.UtcNow));
            Add(command, "createdat", NpgsqlDbType.TimestampTz, DateTime.UtcNow);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var index = (int)reader.GetInt64(0);
                ids[chunk[index - 1].Index - 1] = reader.GetInt64(1).ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        ///     Flattens a chunk into the parallel arrays the statement unnests. Continuation links are
        ///     remapped to the chunk local ordinality, because that is what the statement joins on.
        /// </summary>
        internal static BatchParameters BuildParameters(IReadOnlyList<BatchJobEntry> chunk)
        {
            var count = chunk.Count;
            var localIndex = new Dictionary<int, int>(count);
            var byIndex = new Dictionary<int, BatchJobEntry>(count);
            for (var i = 0; i < count; i++)
            {
                localIndex[chunk[i].Index] = i + 1;
                byIndex[chunk[i].Index] = chunk[i];
            }

            var parameters = new BatchParameters
            {
                InvocationData = new string[count],
                Arguments = new string[count],
                StateNames = new string[count],
                StateReasons = new string[count],
                StateData = new string[count],
                Queues = new string[count],
                SetKeys = new string[count],
                SetPrefixes = new string[count],
                SetScores = new double[count],
                ParentIndexes = new int?[count],
                ContinuationOptions = new int[count],
                UniqueSetKeys = new string[count],
                UniqueFingerprints = new string[count],
                UniqueExpiresAt = new double[count],
                UniqueDuplicates = new bool[count],
                RootIndexes = new int[count]
            };
            var parameterIndexes = new List<int>();
            var parameterNames = new List<string>();
            var parameterValues = new List<string>();

            for (var i = 0; i < count; i++)
            {
                var entry = chunk[i];
                var data = InvocationData.SerializeJob(entry.Job);
                parameters.InvocationData[i] = data.SerializePayload(true);
                parameters.Arguments[i] = data.Arguments;
                parameters.StateNames[i] = entry.State.Name;
                parameters.StateReasons[i] = entry.State.Reason;
                parameters.StateData[i] = SerializationHelper.Serialize(entry.State.SerializeData());
                parameters.Queues[i] = entry.Queue;
                parameters.SetKeys[i] = entry.SetKey;
                parameters.SetPrefixes[i] = entry.SetValuePrefix;
                parameters.SetScores[i] = entry.SetScore;
                parameters.ParentIndexes[i] = entry.ParentIndex.HasValue ? localIndex[entry.ParentIndex.Value] : null;
                parameters.ContinuationOptions[i] = (int)entry.ContinuationOptions;
                parameters.UniqueSetKeys[i] = entry.UniqueSetKey;
                parameters.UniqueFingerprints[i] = entry.UniqueFingerprint;
                parameters.UniqueExpiresAt[i] = entry.UniqueExpiresAt;
                parameters.UniqueDuplicates[i] = entry.UniqueDuplicate;
                parameters.RootIndexes[i] = localIndex[FindRoot(byIndex, entry).Index];
                foreach (var (name, value) in entry.Parameters)
                {
                    parameterIndexes.Add(i + 1);
                    parameterNames.Add(name);
                    parameterValues.Add(value);
                }
            }

            parameters.ParameterIndexes = parameterIndexes.ToArray();
            parameters.ParameterNames = parameterNames.ToArray();
            parameters.ParameterValues = parameterValues.ToArray();
            return parameters;
        }

        /// <summary>
        ///     Walks up the continuation chain. Only chain roots ever carry a fingerprint, so suppressing a
        ///     whole chain is a lookup on its root instead of a recursive query.
        /// </summary>
        private static BatchJobEntry FindRoot(IReadOnlyDictionary<int, BatchJobEntry> byIndex, BatchJobEntry entry)
        {
            var root = entry;
            while (root.ParentIndex.HasValue && byIndex.TryGetValue(root.ParentIndex.Value, out var parent))
                root = parent;
            return root;
        }

        private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object value) =>
            command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });

        internal static string BuildSql(string schema) => $$"""
            WITH "input" AS (
                SELECT *
                FROM unnest(@invocationdata::text[], @arguments::text[], @statename::text[], @statereason::text[],
                            @statedata::text[], @queue::text[], @setkey::text[], @setprefix::text[],
                            @setscore::float8[], @parentidx::int[], @continuationoptions::int[],
                            @unikey::text[], @fingerprint::text[], @uniqueexpiry::float8[],
                            @presuppressed::bool[], @rootidx::int[])
                     WITH ORDINALITY AS t("invocationdata", "arguments", "statename", "statereason",
                                          "statedata", "queue", "setkey", "setprefix",
                                          "setscore", "parentidx", "continuationoptions",
                                          "unikey", "fingerprint", "uniqueexpiry",
                                          "presuppressed", "rootidx", "idx")
            ),
            "claimed" AS (
                INSERT INTO "{{schema}}"."set" ("key", "value", "score", "expireat")
                SELECT "unikey", "fingerprint", "uniqueexpiry", to_timestamp("uniqueexpiry")
                FROM "input" WHERE "fingerprint" IS NOT NULL
                ON CONFLICT ("key", "value") DO UPDATE
                    SET "score" = EXCLUDED."score", "expireat" = EXCLUDED."expireat"
                    WHERE "set"."score" <= @uniquenow
                RETURNING "key", "value"
            ),
            "suppressed" AS (
                SELECT i."idx"
                FROM "input" i JOIN "input" r ON r."idx" = i."rootidx"
                WHERE r."presuppressed"
                   OR (r."fingerprint" IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM "claimed" c
                                       WHERE c."key" = r."unikey" AND c."value" = r."fingerprint"))
            ),
            "accepted" AS (
                SELECT i.* FROM "input" i
                WHERE NOT EXISTS (SELECT 1 FROM "suppressed" s WHERE s."idx" = i."idx")
            ),
            "allocated" AS (
                SELECT "idx",
                       nextval(pg_get_serial_sequence('"{{schema}}"."job"', 'id')::regclass) AS "jobid",
                       nextval(pg_get_serial_sequence('"{{schema}}"."state"', 'id')::regclass) AS "stateid"
                FROM "accepted"
            ),
            "rows" AS (
                SELECT i.*, a."jobid", a."stateid"
                FROM "accepted" i JOIN "allocated" a ON a."idx" = i."idx"
            ),
            "linked" AS (
                SELECT r.*, p."jobid" AS "parentjobid"
                FROM "rows" r LEFT JOIN "rows" p ON p."idx" = r."parentidx"
            ),
            "ins_job" AS (
                INSERT INTO "{{schema}}"."job" ("id", "stateid", "statename", "invocationdata", "arguments", "createdat", "expireat")
                SELECT "jobid", "stateid", "statename", "invocationdata"::jsonb, "arguments"::jsonb, @createdat, NULL
                FROM "linked"
            ),
            "ins_state" AS (
                INSERT INTO "{{schema}}"."state" ("id", "jobid", "name", "reason", "createdat", "data")
                SELECT "stateid", "jobid", "statename", "statereason", @createdat,
                       CASE WHEN "parentjobid" IS NULL THEN "statedata"::jsonb
                            ELSE jsonb_set("statedata"::jsonb, '{ParentId}', to_jsonb("parentjobid"::text)) END
                FROM "linked"
            ),
            "ins_queue" AS (
                INSERT INTO "{{schema}}"."jobqueue" ("jobid", "queue")
                SELECT "jobid", "queue" FROM "linked" WHERE "queue" IS NOT NULL
            ),
            "ins_set" AS (
                INSERT INTO "{{schema}}"."set" ("key", "value", "score")
                SELECT "setkey", COALESCE("setprefix" || ':', '') || "jobid"::text, "setscore"
                FROM "linked" WHERE "setkey" IS NOT NULL
                ON CONFLICT ("key", "value") DO UPDATE SET "score" = EXCLUDED."score"
            ),
            "ins_parameter" AS (
                INSERT INTO "{{schema}}"."jobparameter" ("jobid", "name", "value")
                SELECT r."jobid", p."name", p."value"
                FROM unnest(@paramidx::int[], @paramname::text[], @paramvalue::text[]) AS p("idx", "name", "value")
                JOIN "rows" r ON r."idx" = p."idx"
            ),
            "ins_continuation" AS (
                INSERT INTO "{{schema}}"."jobparameter" ("jobid", "name", "value")
                SELECT c."parentjobid", 'Continuations',
                       jsonb_agg(jsonb_build_object('JobId', c."jobid"::text, 'Options', c."continuationoptions")
                                 ORDER BY c."idx")::text
                FROM "linked" c
                WHERE c."parentjobid" IS NOT NULL
                GROUP BY c."parentjobid"
            )
            SELECT "idx", "jobid" FROM "rows" ORDER BY "idx";
            """;
    }
    /// <summary>Column-oriented representation of a batch chunk, one array per statement parameter.</summary>
    internal sealed class BatchParameters
    {
        public string[] InvocationData { get; init; }
        public string[] Arguments { get; init; }
        public string[] StateNames { get; init; }
        public string[] StateReasons { get; init; }
        public string[] StateData { get; init; }
        public string[] Queues { get; init; }
        public string[] SetKeys { get; init; }
        public string[] SetPrefixes { get; init; }
        public double[] SetScores { get; init; }
        public int?[] ParentIndexes { get; init; }
        public int[] ContinuationOptions { get; init; }
        public int[] ParameterIndexes { get; set; }
        public string[] ParameterNames { get; set; }
        public string[] ParameterValues { get; set; }
        public string[] UniqueSetKeys { get; init; }
        public string[] UniqueFingerprints { get; init; }
        public double[] UniqueExpiresAt { get; init; }
        public bool[] UniqueDuplicates { get; init; }
        public int[] RootIndexes { get; init; }
    }
}
