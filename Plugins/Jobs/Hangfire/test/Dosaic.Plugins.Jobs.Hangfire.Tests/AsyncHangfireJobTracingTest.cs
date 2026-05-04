using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Jobs.Hangfire.Job;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    public class AsyncHangfireJobTracingTest
    {
        private List<Activity> _stopped;
        private ActivityListener _listener;

        [SetUp]
        public void Setup()
        {
            _stopped = [];
            _listener = new ActivityListener
            {
                ShouldListenTo = src => src.Name == Tracing.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => _stopped.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TearDown]
        public void TearDown() => _listener.Dispose();

        [Test]
        public async Task AsyncJobEmitsSpanNamedAfterTypeAndOkStatus()
        {
            var job = new TestJobSuccessAsync(NullLogger.Instance);
            await job.ExecuteAsync(CancellationToken.None);

            _stopped.Should().ContainSingle();
            _stopped[0].Source.Name.Should().Be(Tracing.SourceName);
            _stopped[0].OperationName.Should().Be(typeof(TestJobSuccessAsync).FullName);
            _stopped[0].Status.Should().Be(ActivityStatusCode.Ok);
        }

        [Test]
        public async Task AsyncJobMarksSpanErrorOnException()
        {
            var job = new JobFailsJob(NullLogger.Instance);
            var act = async () => await job.ExecuteAsync(CancellationToken.None);
            await act.Should().ThrowAsync<NotSupportedException>();

            _stopped.Should().ContainSingle();
            _stopped[0].Status.Should().Be(ActivityStatusCode.Error);
        }

        [Test]
        public async Task ParameterizedAsyncJobEmitsSpanWithEnrichedTags()
        {
            var job = new EnrichingParamJob(NullLogger.Instance);
            await job.ExecuteAsync("hello", CancellationToken.None);

            _stopped.Should().ContainSingle();
            _stopped[0].OperationName.Should().Be(typeof(EnrichingParamJob).FullName);
            _stopped[0].GetTagItem("test.value").Should().Be("hello");
        }

        [Test]
        public async Task EnrichActivityRunsForAsyncJob()
        {
            var job = new EnrichingAsyncJob(NullLogger.Instance);
            await job.ExecuteAsync(CancellationToken.None);

            _stopped.Should().ContainSingle();
            _stopped[0].GetTagItem("custom.tag").Should().Be("v");
        }

        private sealed class EnrichingAsyncJob(ILogger logger) : AsyncJob(logger)
        {
            protected override Task<object> ExecuteJobAsync(CancellationToken cancellationToken)
                => Task.FromResult<object>("ok");

            protected override void EnrichActivity(Activity activity)
                => activity?.SetTag("custom.tag", "v");
        }

        private sealed class EnrichingParamJob(ILogger logger) : ParameterizedAsyncJob<string>(logger)
        {
            protected override Task<object> ExecuteJobAsync(string value, CancellationToken cancellationToken)
                => Task.FromResult<object>(value);

            protected override void EnrichActivity(Activity activity, string value)
                => activity?.SetTag("test.value", value);
        }
    }
}
