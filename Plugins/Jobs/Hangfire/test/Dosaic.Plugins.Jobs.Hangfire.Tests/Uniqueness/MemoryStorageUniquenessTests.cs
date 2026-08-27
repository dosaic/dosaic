using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Attributes;
using Dosaic.Plugins.Jobs.Hangfire.Batching;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.States;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Uniqueness
{
    /// <summary>
    ///     Runs the real Hangfire filter pipeline against the in-memory storage, which is the fallback the
    ///     claim takes when the storage has no bulk statement.
    /// </summary>
    [NonParallelizable]
    public class MemoryStorageUniquenessTests
    {
        private const string Queue = "memory-unique";
        private MemoryStorage _storage;
        private BackgroundJobClient _client;

        [SetUp]
        public void Up()
        {
            _storage = new MemoryStorage();
            GlobalConfiguration.Configuration.UseStorage(_storage);
            _client = new BackgroundJobClient(_storage);
        }

        private string Create(string payload) =>
            _client.Create(
                global::Hangfire.Common.Job.FromExpression<MemoryUniqueJob>(x =>
                    x.ExecuteAsync(payload, CancellationToken.None)),
                new EnqueuedState());

        private string StateOf(string jobId)
        {
            using var connection = _storage.GetConnection();
            return connection.GetJobData(jobId).State;
        }

        [Test]
        public void TheFirstJobIsEnqueuedAndTheDuplicateIsDeleted()
        {
            StateOf(Create("same")).Should().Be("Enqueued");
            StateOf(Create("same")).Should().Be("Deleted");
        }

        [Test]
        public void DifferentArgumentsAreNotDuplicates()
        {
            StateOf(Create("a")).Should().Be("Enqueued");
            StateOf(Create("b")).Should().Be("Enqueued");
        }

        [Test]
        public void TheAttributeStillOverridesTheQueue()
        {
            var id = Create("queued");
            using var connection = _storage.GetConnection();
            connection.GetStateData(id).Data["Queue"].Should().Be(Queue);
        }

        [Test]
        public void TheClaimIsReleasedWhenTheJobLeavesTheQueue()
        {
            var first = Create("release");
            StateOf(Create("release")).Should().Be("Deleted");

            new BackgroundJobClient(_storage).ChangeState(first, new SucceededState(null, 0, 0));

            StateOf(Create("release")).Should().Be("Enqueued", "the fingerprint is free again");
        }

        [Test]
        public async Task TheBatchFallbackDeduplicatesThroughTheFilterPipeline()
        {
            var batch = new JobBatch(new BackgroundJobClientBatchDispatcher(_client));
            batch.Enqueue<MemoryUniqueJob, string>("batched");
            batch.Enqueue<MemoryUniqueJob, string>("batched");
            var ids = await batch.SaveAsync();

            ids[0].Should().NotBeNull();
            ids[1].Should().BeNull("the batch itself already suppresses the second occurrence");
            StateOf(ids[0]).Should().Be("Enqueued");
            StateOf(Create("batched")).Should().Be("Deleted", "the batch really took the claim");
        }
    }

    [UniquePerQueue("memory-unique")]
    public class MemoryUniqueJob : IParameterizedAsyncJob<string>
    {
        public Task<object> ExecuteAsync(string value, CancellationToken jobCancellationToken = default) =>
            Task.FromResult<object>(value);

        public void Dispose() => GC.SuppressFinalize(this);
    }
}
