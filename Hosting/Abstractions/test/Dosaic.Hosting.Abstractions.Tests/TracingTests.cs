using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests
{
    public class TracingTests
    {
        private ActivityListener _listener;
        private IList<Activity> _activities;

        [SetUp]
        public void Setup()
        {
            _activities = [];
            _listener = new ActivityListener
            {
                ShouldListenTo = src => src.Name == Tracing.SourceName,
                ActivityStopped = activity => _activities.Add(activity),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TearDown]
        public void TearDown()
        {
            _listener.Dispose();
        }

        [Test]
        public void SourceUsesTheSharedName()
        {
            Tracing.Source.Name.Should().Be(Tracing.SourceName);
            Tracing.SourceName.Should().Be("Dosaic");
        }

        [Test]
        public void StartActivityUsesTheSharedSource()
        {
            using (Tracing.StartActivity("test-span"))
            {
                // span lives for the duration of using
            }

            _activities.Should().HaveCount(1);
            _activities[0].Source.Name.Should().Be(Tracing.SourceName);
            _activities[0].OperationName.Should().Be("test-span");
        }

        [Test]
        public async Task TrackStatusAsyncWithResultSetsOk()
        {
            var result = await Tracing.TrackStatusAsync(activity =>
            {
                activity.SetTags(new() { { "hello", "world" } });
                return Task.FromResult(42);
            }, "with-result");

            result.Should().Be(42);
            _activities.Should().HaveCount(1);
            _activities[0].Status.Should().Be(ActivityStatusCode.Ok);
            _activities[0].Tags.Should().Contain(t => t.Key == "hello" && t.Value == "world");
        }

        [Test]
        public async Task TrackStatusAsyncWithoutResultSetsOk()
        {
            await Tracing.TrackStatusAsync(activity =>
            {
                activity.SetTag("k", "v");
                return Task.CompletedTask;
            }, "void");

            _activities.Should().HaveCount(1);
            _activities[0].Status.Should().Be(ActivityStatusCode.Ok);
        }

        [Test]
        public async Task TrackStatusAsyncWithResultSetsErrorOnException()
        {
            var act = async () => await Tracing.TrackStatusAsync<int>(activity =>
            {
                activity.SetTag("hello", "world");
                throw new InvalidOperationException("boom");
            }, "fails");

            await act.Should().ThrowAsync<InvalidOperationException>();
            _activities.Should().HaveCount(1);
            _activities[0].Status.Should().Be(ActivityStatusCode.Error);
            _activities[0].StatusDescription.Should().Be("boom");
        }

        [Test]
        public async Task TrackStatusAsyncWithoutResultSetsErrorOnException()
        {
            var act = async () => await Tracing.TrackStatusAsync(activity =>
            {
                activity.SetTag("hello", "world");
                throw new InvalidOperationException("boom");
            }, "fails-void");

            await act.Should().ThrowAsync<InvalidOperationException>();
            _activities.Should().HaveCount(1);
            _activities[0].Status.Should().Be(ActivityStatusCode.Error);
        }

        [Test]
        public void StartActivityWithFullSignatureForwardsArguments()
        {
            using var parent = Tracing.StartActivity("parent");
            using var act = Tracing.StartActivity(
                "child",
                ActivityKind.Consumer,
                parent!.Context,
                new Dictionary<string, object> { ["k"] = "v" });

            act.Should().NotBeNull();
            act!.Kind.Should().Be(ActivityKind.Consumer);
            act.GetTagItem("k").Should().Be("v");
        }

        [Test]
        public void ActivityExtensionsAreNullSafe()
        {
            Activity a = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            a.SetTags(new() { { "key", "value" } }).Should().BeNull();
            // ReSharper disable once ExpressionIsAlwaysNull
            a.SetErrorStatus(new Exception("test")).Should().BeNull();
            // ReSharper disable once ExpressionIsAlwaysNull
            a.SetOkStatus().Should().BeNull();
        }

        [Test]
        public void SetTagsAppliesPrefix()
        {
            using var act = Tracing.StartActivity("tagged");
            act!.SetTags(new() { { "a", "1" }, { "b", "2" } }, prefix: "x.");
            act.GetTagItem("x.a").Should().Be("1");
            act.GetTagItem("x.b").Should().Be("2");
        }
    }
}
