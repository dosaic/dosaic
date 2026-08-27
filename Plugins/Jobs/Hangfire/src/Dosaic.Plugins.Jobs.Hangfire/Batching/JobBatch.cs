using System.Globalization;
using System.Reflection;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using Hangfire.States;

namespace Dosaic.Plugins.Jobs.Hangfire.Batching
{
    internal sealed class JobBatch : IJobBatch
    {
        /// <summary>
        ///     Placeholder written into <see cref="AwaitingState" /> while the antecedent job id is still unknown.
        /// </summary>
        internal const string PendingParentId = "0";

        private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly IJobBatchDispatcher _dispatcher;
        private readonly List<BatchJobEntry> _entries = [];
        private readonly List<BatchItem> _items = [];
        private readonly HashSet<string> _claimedFingerprints = [];
        private bool _saved;

        public JobBatch(IJobBatchDispatcher dispatcher) => _dispatcher = dispatcher;

        public int Count => _entries.Count;

        public IJobBatchItem Enqueue<TJob>(string queue = EnqueuedState.DefaultQueue) where TJob : IAsyncJob =>
            Add(global::Hangfire.Common.Job.FromExpression<TJob>(x => x.ExecuteAsync(CancellationToken.None)),
                new EnqueuedState(queue), queue, null, null, 0, null, default);

        public IJobBatchItem Enqueue<TJob, TJobParams>(TJobParams parameters, string queue = EnqueuedState.DefaultQueue)
            where TJob : IParameterizedAsyncJob<TJobParams> =>
            Add(global::Hangfire.Common.Job.FromExpression<TJob>(x => x.ExecuteAsync(parameters, CancellationToken.None)),
                new EnqueuedState(queue), queue, null, null, 0, null, default);

        public IJobBatchItem Schedule<TJob>(TimeSpan delay, string queue = EnqueuedState.DefaultQueue)
            where TJob : IAsyncJob => ScheduleAt<TJob>(DateTimeOffset.UtcNow.Add(delay), queue);

        public IJobBatchItem Schedule<TJob, TJobParams>(TJobParams parameters, TimeSpan delay,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams> =>
            ScheduleAt<TJob, TJobParams>(parameters, DateTimeOffset.UtcNow.Add(delay), queue);

        public IJobBatchItem ScheduleAt<TJob>(DateTimeOffset enqueueAt, string queue = EnqueuedState.DefaultQueue)
            where TJob : IAsyncJob =>
            AddScheduled(global::Hangfire.Common.Job.FromExpression<TJob>(x => x.ExecuteAsync(CancellationToken.None)),
                enqueueAt, queue);

        public IJobBatchItem ScheduleAt<TJob, TJobParams>(TJobParams parameters, DateTimeOffset enqueueAt,
            string queue = EnqueuedState.DefaultQueue) where TJob : IParameterizedAsyncJob<TJobParams> =>
            AddScheduled(
                global::Hangfire.Common.Job.FromExpression<TJob>(x => x.ExecuteAsync(parameters, CancellationToken.None)),
                enqueueAt, queue);

        public async Task<IReadOnlyList<string>> SaveAsync(CancellationToken cancellationToken = default)
        {
            if (_saved) throw new InvalidOperationException("This job batch has already been saved.");
            if (_entries.Count == 0)
            {
                _saved = true;
                return [];
            }

            var ids = await _dispatcher.DispatchAsync(_entries, cancellationToken).ConfigureAwait(false);
            if (ids.Count != _entries.Count)
                throw new InvalidOperationException(
                    $"Job batch dispatcher returned {ids.Count} job ids for {_entries.Count} jobs.");
            for (var i = 0; i < ids.Count; i++)
            {
                _items[i].Id = ids[i];
                _items[i].IsSuppressed = ids[i] is null;
            }

            _saved = true;
            return ids;
        }

        public IReadOnlyList<string> Save() => SaveAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        private IJobBatchItem AddScheduled(global::Hangfire.Common.Job job, DateTimeOffset enqueueAt, string queue)
        {
            var state = new ScheduledState(enqueueAt.UtcDateTime);
            return Add(job, state, null, "schedule", queue, ToTimestamp(enqueueAt.UtcDateTime), null, default);
        }

        private IJobBatchItem AddContinuation(global::Hangfire.Common.Job job, int parentIndex, string queue,
            JobContinuationOptions options)
        {
            // ParentId is unknown until the batch is written — the dispatcher patches it in.
            var state = new AwaitingState(PendingParentId, new EnqueuedState(queue), options);
            return Add(job, state, null, "awaiting", null, ToTimestamp(DateTime.UtcNow), parentIndex, options);
        }

        private IJobBatchItem Add(global::Hangfire.Common.Job job, IState state, string queue, string setKey,
            string setValuePrefix, double setScore, int? parentIndex, JobContinuationOptions options)
        {
            if (_saved) throw new InvalidOperationException("This job batch has already been saved.");
            var index = _entries.Count + 1;
            var unique = job.Type.GetCustomAttribute<UniquePerQueueAttribute>(true);
            var parameters = CaptureCulture();
            if (unique is not null) ApplyUniqueQueue(unique, state, ref queue, ref setValuePrefix);
            var fingerprint = GetFingerprint(unique, state, job);
            var duplicate = fingerprint is not null && !_claimedFingerprints.Add(fingerprint);
            if (fingerprint is not null && !duplicate)
                parameters[JobFingerprint.ClaimParameterName] = $"\"{fingerprint}\"";
            _entries.Add(new BatchJobEntry
            {
                Index = index,
                Job = job,
                State = state,
                Queue = queue,
                SetKey = setKey,
                SetValuePrefix = setValuePrefix,
                SetScore = setScore,
                ParentIndex = parentIndex,
                ContinuationOptions = options,
                UniqueSetKey = unique is null ? null : JobFingerprint.SetKey(unique.Queue),
                UniqueFingerprint = duplicate ? null : fingerprint,
                UniqueExpiresAt = fingerprint is null || duplicate
                    ? 0
                    : ToTimestamp(DateTime.UtcNow) + TimeSpan.FromMinutes(unique.ClaimTimeoutInMinutes).TotalSeconds,
                UniqueDuplicate = duplicate,
                Parameters = parameters
            });
            var item = new BatchItem(this, index);
            _items.Add(item);
            return item;
        }

        /// <summary>
        ///     The attribute owns the queue of the jobs it guards, the same way it overrides the queue of the
        ///     elected <see cref="EnqueuedState" /> outside the batch API.
        /// </summary>
        private static void ApplyUniqueQueue(UniquePerQueueAttribute unique, IState state, ref string queue,
            ref string setValuePrefix)
        {
            switch (state)
            {
                case EnqueuedState enqueuedState:
                    enqueuedState.Queue = unique.Queue;
                    queue = unique.Queue;
                    break;
                case ScheduledState:
                    setValuePrefix = unique.Queue;
                    break;
                case AwaitingState { NextState: EnqueuedState nextState }:
                    nextState.Queue = unique.Queue;
                    break;
            }
        }

        /// <summary>
        ///     Continuations are written in <see cref="AwaitingState" /> and only become enqueued much later,
        ///     so they are left to the filter pipeline instead of being claimed up front.
        /// </summary>
        private static string GetFingerprint(UniquePerQueueAttribute unique, IState state,
            global::Hangfire.Common.Job job)
        {
            if (unique is null) return null;
            var claims = state is EnqueuedState || (unique.CheckScheduledJobs && state is ScheduledState);
            return claims ? JobFingerprint.Compute(job, unique.Queue) : null;
        }

        private static IDictionary<string, string> CaptureCulture()
        {
            // Hangfire's CaptureCultureAttribute writes both parameters unconditionally,
            // including the empty name of the invariant culture - keep parity.
            return new Dictionary<string, string>
            {
                ["CurrentCulture"] = $"\"{CultureInfo.CurrentCulture.Name}\"",
                ["CurrentUICulture"] = $"\"{CultureInfo.CurrentUICulture.Name}\""
            };
        }

        private static double ToTimestamp(DateTime value) => (long)(value.ToUniversalTime() - _epoch).TotalSeconds;

        private sealed class BatchItem : IJobBatchItem
        {
            private readonly JobBatch _batch;

            public BatchItem(JobBatch batch, int index)
            {
                _batch = batch;
                Index = index;
            }

            public int Index { get; }
            public string Id { get; internal set; }
            public bool IsSuppressed { get; internal set; }

            public IJobBatchItem ContinueWith<TJob>(string queue = EnqueuedState.DefaultQueue,
                JobContinuationOptions options = JobContinuationOptions.OnlyOnSucceededState) where TJob : IAsyncJob =>
                _batch.AddContinuation(
                    global::Hangfire.Common.Job.FromExpression<TJob>(x => x.ExecuteAsync(CancellationToken.None)),
                    Index, queue, options);

            public IJobBatchItem ContinueWith<TJob, TJobParams>(TJobParams parameters,
                string queue = EnqueuedState.DefaultQueue,
                JobContinuationOptions options = JobContinuationOptions.OnlyOnSucceededState)
                where TJob : IParameterizedAsyncJob<TJobParams> =>
                _batch.AddContinuation(
                    global::Hangfire.Common.Job.FromExpression<TJob>(x =>
                        x.ExecuteAsync(parameters, CancellationToken.None)), Index, queue, options);
        }
    }
}
