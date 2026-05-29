using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Extensions.Tracing.FabricFixture;
using NUnit.Framework;

namespace Dosaic.Extensions.Tracing.Tests
{
    // Integration tests for TracingFabric: the FabricFixture project is compiled with
    // DosaicTracingMode=AllPublic and DosaicTracingExclude=**.Excluded.*, so these assert
    // what the global fabric wove without any [Trace] attributes in the fixture.
    [TestFixture]
    public class TracingFabricTests
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

        private bool Traced(string name) => _activities.Any(a => a.DisplayName == name);

        [Test]
        public void AllPublicTracesPublicInstanceMethods()
        {
            new AutoTracedService().PublicMethod("x").Should().Be("x");
            Traced("AutoTracedService.PublicMethod").Should().BeTrue();
        }

        [Test]
        public async Task AllPublicTracesPublicAsyncMethods()
        {
            (await new AutoTracedService().PublicAsyncMethod(1)).Should().Be(2);
            Traced("AutoTracedService.PublicAsyncMethod").Should().BeTrue();
        }

        [Test]
        public void AllPublicDoesNotTracePrivateMethods()
        {
            new AutoTracedService().PublicMethod("x");
            Traced("AutoTracedService.Helper").Should().BeFalse();
        }

        [Test]
        public void HonorsNoTraceOnMethod()
        {
            new AutoTracedService().OptedOutMethod("x").Should().Be("x");
            Traced("AutoTracedService.OptedOutMethod").Should().BeFalse();
        }

        [Test]
        public void HonorsNoTraceOnClass()
        {
            new FullyOptedOutService().Method("x").Should().Be("x");
            Traced("FullyOptedOutService.Method").Should().BeFalse();
        }

        [Test]
        public void HonorsExcludeGlob()
        {
            new FabricFixture.Excluded.ExcludedService().Method("x").Should().Be("x");
            Traced("ExcludedService.Method").Should().BeFalse();
        }
    }
}
