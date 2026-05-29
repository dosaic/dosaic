using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Tracing.Tests
{
    [TestFixture]
    public class TraceAttributeTests
    {
        private List<Activity> _activities;
        private ActivityListener _listener;

        [SetUp]
        public void Setup()
        {
            _activities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Dosaic",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TearDown]
        public void TearDown() => _listener.Dispose();

        private Activity Single(string name) => _activities.Single(a => a.DisplayName == name);

        [Test]
        public void TracesSyncMethodWithTypeQualifiedSpanName()
        {
            new SampleService().Echo("hello").Should().Be("hello");
            Single("SampleService.Echo").Should().NotBeNull();
        }

        [Test]
        public async Task TracesAsyncMethod()
        {
            (await new SampleService().AddAsync(2, 3)).Should().Be(5);
            Single("SampleService.AddAsync").Should().NotBeNull();
        }

        [Test]
        public void DoesNotCaptureArgumentsByDefaultOnHappyPath()
        {
            new SampleService().Echo("hello");
            Single("SampleService.Echo").Tags.Should().NotContain(t => t.Key.StartsWith("arg."));
        }

        [Test]
        public void RecordsErrorStatusAndCapturesArgumentsOnException()
        {
            var act = () => new SampleService().Boom("kaboom");
            act.Should().Throw<InvalidOperationException>().WithMessage("kaboom");
            var activity = Single("SampleService.Boom");
            activity.Status.Should().Be(ActivityStatusCode.Error);
            activity.TagObjects.Should().Contain(t => t.Key == "arg.reason");
        }

        [Test]
        public void HonorsNoTraceOnMethodOfTracedClass()
        {
            new SampleService().Untraced("x").Should().Be("x");
            _activities.Should().NotContain(a => a.DisplayName == "SampleService.Untraced");
        }

        [Test]
        public void CapturesArgumentsViaToStringAndSkipsFilteredAndNoCaptureParameters()
        {
            new ToStringCapturingService().Build(7, "secret");
            var activity = Single("ToStringCapturingService.Build");
            activity.GetTagItem("arg.id").Should().Be("7");
            activity.GetTagItem("arg.secret").Should().BeNull();
            activity.GetTagItem("arg.ct").Should().BeNull();
        }

        [Test]
        public void CapturesArgumentsAsJson()
        {
            new JsonCapturingService().Handle(new Payload("alice", 3));
            var activity = Single("JsonCapturingService.Handle");
            activity.GetTagItem("arg.payload").Should().Be("{\"name\":\"alice\",\"count\":3}");
        }

        [Test]
        public void CaptureFailureDoesNotThrowAndRecordsFallbackTag()
        {
            var node = new Node();
            node.Self = node;
            new JsonCapturingService().HandleCyclic(node).Should().Be("ok");
            var activity = Single("JsonCapturingService.HandleCyclic");
            activity.GetTagItem("arg.node").Should().BeOfType<string>().Which.Should().StartWith("<");
        }

        [Test]
        public void CaptureFailureOnErrorPathDoesNotMaskOriginalException()
        {
            var node = new Node();
            node.Self = node;
            var act = () => new ErrorCaptureService().BoomWithCyclic(node);
            act.Should().Throw<InvalidOperationException>().WithMessage("kaboom");
            var activity = Single("ErrorCaptureService.BoomWithCyclic");
            activity.Status.Should().Be(ActivityStatusCode.Error);
            activity.GetTagItem("arg.node").Should().BeOfType<string>().Which.Should().StartWith("<");
        }

        [Test]
        public void TracesMethodLevelAttributeOnly()
        {
            var service = new MethodLevelService();
            service.Traced("a");
            service.NotTraced("b");
            _activities.Should().ContainSingle(a => a.DisplayName == "MethodLevelService.Traced");
            _activities.Should().NotContain(a => a.DisplayName == "MethodLevelService.NotTraced");
        }
    }
}
