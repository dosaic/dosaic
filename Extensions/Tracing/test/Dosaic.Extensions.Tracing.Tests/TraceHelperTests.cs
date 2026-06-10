using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Tracing.Tests
{
    [TestFixture]
    public class TraceHelperTests
    {
        private ActivityListener _listener;
        private ActivitySource _source;

        [SetUp]
        public void Setup()
        {
            _source = new ActivitySource("Dosaic");
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Dosaic",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TearDown]
        public void TearDown()
        {
            _listener.Dispose();
            _source.Dispose();
        }

        [Test]
        public void CurrentReturnsAmbientActivity()
        {
            using var activity = _source.StartActivity("test");
            Dosaic.Hosting.Abstractions.Tracing.Current.Should().BeSameAs(Activity.Current);
        }

        [Test]
        public void CurrentIsNullWhenNoActivity()
        {
            Activity.Current.Should().BeNull();
            Dosaic.Hosting.Abstractions.Tracing.Current.Should().BeNull();
        }

        [Test]
        public void TagSetsTagOnCurrentActivity()
        {
            using var activity = _source.StartActivity("test");
            Dosaic.Hosting.Abstractions.Tracing.Tag("key", "value");
            activity.GetTagItem("key").Should().Be("value");
        }

        [Test]
        public void EventAddsEventToCurrentActivity()
        {
            using var activity = _source.StartActivity("test");
            Dosaic.Hosting.Abstractions.Tracing.Event("happened", new KeyValuePair<string, object>("k", "v"));
            activity.Events.Should().ContainSingle(e => e.Name == "happened");
        }

        [Test]
        public void ErrorSetsErrorStatusOnCurrentActivity()
        {
            using var activity = _source.StartActivity("test");
            Dosaic.Hosting.Abstractions.Tracing.Error(new InvalidOperationException("boom"));
            activity.Status.Should().Be(ActivityStatusCode.Error);
            activity.StatusDescription.Should().Be("boom");
        }

        [Test]
        public void LinkAddsLinkToCurrentActivity()
        {
            var target = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var activity = _source.StartActivity("test");
            Dosaic.Hosting.Abstractions.Tracing.Link(target, "related");
            activity.Links.Should().ContainSingle(l => l.Context == target);
        }

        [Test]
        public void HelpersAreNoOpsWithoutCurrentActivity()
        {
            var act = () =>
            {
                Dosaic.Hosting.Abstractions.Tracing.Tag("k", "v");
                Dosaic.Hosting.Abstractions.Tracing.Event("e");
                Dosaic.Hosting.Abstractions.Tracing.Error(new Exception());
                Dosaic.Hosting.Abstractions.Tracing.Link(default);
            };
            act.Should().NotThrow();
        }
    }
}
