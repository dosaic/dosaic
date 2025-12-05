using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Extensions
{
    public class TracingExtensionTests
    {
        private ActivitySource _source;
        private ActivityListener _listener;
        private IList<Activity> _activities;

        [SetUp]
        public void Setup()
        {
            _activities = [];
            _listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                ActivityStopped = activity => _activities.Add(activity),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
            _source = new ActivitySource("Dosaic.Hosting.Abstractions.Tests");
        }

        [TearDown]
        public void Down()
        {
            _listener.Dispose();
        }

        [Test]
        public async Task TrackStatusAsyncWorks()
        {
            await _source.TrackStatusAsync(activity =>
            {
                activity.SetTags(new() { { "hello", "world" }, { "custom", "tag" } });
                return Task.FromResult(42);
            });
            _activities.Should().HaveCount(1);
            var act = _activities[0];
            act.Status.Should().Be(ActivityStatusCode.Ok);
            act.Tags.Should().HaveCount(2);
            act.Tags.Should().Contain(x => x.Key == "hello" && x.Value == "world");
            act.Tags.Should().Contain(x => x.Key == "custom" && x.Value == "tag");
        }

        [Test]
        public async Task TrackStatusAsyncWorksWithoutAResult()
        {
            await _source.TrackStatusAsync(activity =>
            {
                activity.SetTags(new() { { "hello", "world" }, { "custom", "tag" } });
                return Task.CompletedTask;
            });
            _activities.Should().HaveCount(1);
            var act = _activities[0];
            act.Status.Should().Be(ActivityStatusCode.Ok);
            act.Tags.Should().HaveCount(2);
            act.Tags.Should().Contain(x => x.Key == "hello" && x.Value == "world");
            act.Tags.Should().Contain(x => x.Key == "custom" && x.Value == "tag");
        }

        [Test]
        public async Task TrackStatusAsyncWorksWithExceptions()
        {
            await _source.Invoking(async x =>
            {
                await x.TrackStatusAsync<int>(activity =>
                {
                    activity.SetTag("hello", "world");
                    throw new InvalidCastException("test");
                });
            }).Should().ThrowAsync<InvalidCastException>();
            _activities.Should().HaveCount(1);
            var act = _activities[0];
            act.Status.Should().Be(ActivityStatusCode.Error);
            act.StatusDescription.Should().Be("test");
            act.Tags.Should().HaveCount(1);
            act.Tags.Should().Contain(x => x.Key == "hello" && x.Value == "world");
        }

        [Test]
        public async Task TrackStatusAsyncWorksWithoutAResultWithExceptions()
        {
            await _source.Invoking(async x =>
            {
                await x.TrackStatusAsync(activity =>
                {
                    activity.SetTag("hello", "world");
                    throw new InvalidCastException("test");
                });
            }).Should().ThrowAsync<InvalidCastException>();
            _activities.Should().HaveCount(1);
            var act = _activities[0];
            act.Status.Should().Be(ActivityStatusCode.Error);
            act.StatusDescription.Should().Be("test");
            act.Tags.Should().HaveCount(1);
            act.Tags.Should().Contain(x => x.Key == "hello" && x.Value == "world");
        }

        [Test]
        public void ActivityExtensionsAreNullSafe()
        {
            Activity a = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            a!.SetTags(new() { { "key", "value" } }).Should().BeNull();
            // ReSharper disable once ExpressionIsAlwaysNull
            a!.SetErrorStatus(new Exception("test")).Should().BeNull();
            // ReSharper disable once ExpressionIsAlwaysNull
            a.SetOkStatus().Should().BeNull();
        }
    }
}
