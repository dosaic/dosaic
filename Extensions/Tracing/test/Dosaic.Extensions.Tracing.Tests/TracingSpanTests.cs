using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;
using DosaicTracing = Dosaic.Hosting.Abstractions.Tracing;

namespace Dosaic.Extensions.Tracing.Tests
{
    [TestFixture]
    public class TracingSpanTests
    {
        private List<Activity> _activities;
        private ActivityListener _listener;

        [SetUp]
        public void Setup()
        {
            _activities = new List<Activity>();
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == DosaicTracing.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        [TearDown]
        public void TearDown() => _listener.Dispose();

        private Activity Single(string name) => _activities.Single(a => a.DisplayName == name);

        [Test]
        public void StartSpanNamesSpanAfterOwnerTypeAndCallingMember()
        {
            new SpanService().Echo("hello").Should().Be("hello");
            var activity = Single("SpanService.Echo");
            activity.Source.Name.Should().Be(DosaicTracing.SourceName);
            activity.Kind.Should().Be(ActivityKind.Internal);
            activity.GetTagItem("value.length").Should().Be(5);
        }

        [Test]
        public void StartSpanUsesGenericDisplayNameOfOwner()
        {
            new GenericSpanService<int>().Run();
            Single("GenericSpanService<Int32>.Run").Should().NotBeNull();
        }

        [Test]
        public void StartSpanWithTypeArgumentWorksForStaticMembers()
        {
            SpanService.StaticWork();
            Single("SpanService.StaticWork").Should().NotBeNull();
        }

        [Test]
        public void StartSpanHonorsActivityKind()
        {
            new SpanService().Consume();
            Single("SpanService.Consume").Kind.Should().Be(ActivityKind.Consumer);
        }

        [Test]
        public void TraceWrapsActionAndPassesActivity()
        {
            new SpanService().Act();
            Single("SpanService.Act").GetTagItem("acted").Should().Be(true);
        }

        [Test]
        public void TraceReturnsFunctionResult()
        {
            new SpanService().Compute(20).Should().Be(21);
            Single("SpanService.Compute").Status.Should().Be(ActivityStatusCode.Unset);
        }

        [Test]
        public void TraceRecordsErrorAndRethrows()
        {
            var act = () => new SpanService().Boom("kaboom");
            act.Should().Throw<InvalidOperationException>().WithMessage("kaboom");
            var activity = Single("SpanService.Boom");
            activity.Status.Should().Be(ActivityStatusCode.Error);
            activity.StatusDescription.Should().Be("kaboom");
        }

        [Test]
        public void TraceWithResultRecordsErrorAndRethrows()
        {
            var act = () => new SpanService().BoomWithResult();
            act.Should().Throw<InvalidOperationException>();
            Single("SpanService.BoomWithResult").Status.Should().Be(ActivityStatusCode.Error);
        }

        [Test]
        public async Task TraceAsyncReturnsResult()
        {
            (await new SpanService().AddAsync(2, 3)).Should().Be(5);
            Single("SpanService.AddAsync").Status.Should().Be(ActivityStatusCode.Unset);
        }

        [Test]
        public async Task TraceAsyncWithoutResultCompletes()
        {
            await new SpanService().WaitAsync();
            Single("SpanService.WaitAsync").Should().NotBeNull();
        }

        [Test]
        public async Task TraceAsyncRecordsErrorAndRethrows()
        {
            var act = () => new SpanService().BoomAsync();
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("async kaboom");
            Single("SpanService.BoomAsync").Status.Should().Be(ActivityStatusCode.Error);
        }

        [Test]
        public async Task TraceAsyncWithResultRecordsErrorAndRethrows()
        {
            var act = () => new SpanService().BoomWithResultAsync();
            await act.Should().ThrowAsync<InvalidOperationException>();
            Single("SpanService.BoomWithResultAsync").Status.Should().Be(ActivityStatusCode.Error);
        }

        [Test]
        public void NestedSpansAreParentedToTheOuterSpan()
        {
            new SpanService().Outer();
            var outer = Single("SpanService.Outer");
            Single("SpanService.Echo").ParentSpanId.Should().Be(outer.SpanId);
        }

        private class SpanService
        {
            public string Echo(string value)
            {
                using var activity = DosaicTracing.StartSpan(this);
                DosaicTracing.Tag("value.length", value.Length);
                return value;
            }

            public static void StaticWork()
            {
                using var activity = DosaicTracing.StartSpan<SpanService>();
            }

            public void Consume()
            {
                using var activity = DosaicTracing.StartSpan(this, kind: ActivityKind.Consumer);
            }

            public void Act() => DosaicTracing.Trace(this, activity => { activity?.SetTag("acted", true); });

            public int Compute(int value) => DosaicTracing.Trace(this, _ => value + 1);

            public void Boom(string reason)
                => DosaicTracing.Trace(this, _ => { throw new InvalidOperationException(reason); });

            public int BoomWithResult()
                => DosaicTracing.Trace<int>(this, _ => throw new InvalidOperationException());

            public Task<int> AddAsync(int a, int b) => DosaicTracing.TraceAsync(this, async _ =>
            {
                await Task.Yield();
                return a + b;
            });

            public Task WaitAsync() => DosaicTracing.TraceAsync(this, async _ => await Task.Yield());

            public Task BoomAsync() => DosaicTracing.TraceAsync(this, async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async kaboom");
            });

            public Task<int> BoomWithResultAsync() => DosaicTracing.TraceAsync<int>(this, async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException();
            });

            public void Outer()
            {
                using var activity = DosaicTracing.StartSpan(this);
                Echo("inner");
            }
        }

        private class GenericSpanService<T>
        {
            public void Run()
            {
                using var activity = DosaicTracing.StartSpan(this);
            }
        }
    }
}
