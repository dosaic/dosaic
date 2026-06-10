using System.Diagnostics;
using AwesomeAssertions;
using Dosaic.Testing.NUnit;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests
{
    public class TracingTests
    {
        [SetUp]
        public void Setup() => ActivityTestBootstrapper.Setup();

        [Test]
        public void SourceUsesTheSharedName()
        {
            Tracing.Source.Name.Should().Be(Tracing.SourceName);
            Tracing.SourceName.Should().Be("Dosaic");
        }

        [Test]
        public void StartActivityUsesTheSharedSource()
        {
            using var act = Tracing.StartActivity("test-span");

            act.Should().NotBeNull();
            act!.Source.Name.Should().Be(Tracing.SourceName);
            act.OperationName.Should().Be("test-span");
        }

        [Test]
        public async Task TrackStatusAsyncWithResultSetsOk()
        {
            Activity captured = null;
            var result = await Tracing.TrackStatusAsync(activity =>
            {
                captured = activity;
                activity.SetTags(new() { { "hello", "world" } });
                return Task.FromResult(42);
            }, "with-result");

            result.Should().Be(42);
            captured.Should().NotBeNull();
            captured!.Status.Should().Be(ActivityStatusCode.Ok);
            captured.Tags.Should().Contain(t => t.Key == "hello" && t.Value == "world");
        }

        [Test]
        public async Task TrackStatusAsyncWithoutResultSetsOk()
        {
            Activity captured = null;
            await Tracing.TrackStatusAsync(activity =>
            {
                captured = activity;
                activity.SetTag("k", "v");
                return Task.CompletedTask;
            }, "void");

            captured.Should().NotBeNull();
            captured!.Status.Should().Be(ActivityStatusCode.Ok);
        }

        [Test]
        public async Task TrackStatusAsyncWithResultSetsErrorOnException()
        {
            Activity captured = null;
            var act = async () => await Tracing.TrackStatusAsync<int>(activity =>
            {
                captured = activity;
                activity.SetTag("hello", "world");
                throw new InvalidOperationException("boom");
            }, "fails");

            await act.Should().ThrowAsync<InvalidOperationException>();
            captured.Should().NotBeNull();
            captured!.Status.Should().Be(ActivityStatusCode.Error);
            captured.StatusDescription.Should().Be("boom");
        }

        [Test]
        public async Task TrackStatusAsyncWithoutResultSetsErrorOnException()
        {
            Activity captured = null;
            var act = async () => await Tracing.TrackStatusAsync(activity =>
            {
                captured = activity;
                activity.SetTag("hello", "world");
                throw new InvalidOperationException("boom");
            }, "fails-void");

            await act.Should().ThrowAsync<InvalidOperationException>();
            captured.Should().NotBeNull();
            captured!.Status.Should().Be(ActivityStatusCode.Error);
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

        [Test]
        public void DisplayNameStripsArityAndRendersGenericArguments()
        {
            typeof(int).DisplayName().Should().Be("Int32");
            typeof(List<string>).DisplayName().Should().Be("List<String>");
            typeof(Dictionary<string, int>).DisplayName().Should().Be("Dictionary<String, Int32>");
            typeof(List<Dictionary<string, int>>).DisplayName().Should().Be("List<Dictionary<String, Int32>>");
            ((Type)null).DisplayName().Should().BeNull();
        }

        [Test]
        public void DisplayNameWithFullNameIncludesNamespace()
        {
            typeof(string).DisplayName(fullName: true).Should().Be("System.String");
            typeof(List<string>).DisplayName(fullName: true)
                .Should().Be("System.Collections.Generic.List<System.String>");
        }

        [Test]
        public void SuppressForLinkingCapturesTraceParentAndRestoresOnDispose()
        {
            using var act = Tracing.StartActivity("sender");
            act.Should().NotBeNull();
            var expected = act!.Id;

            using (var scope = Tracing.SuppressForLinking())
            {
                Activity.Current.Should().BeNull("the ambient activity is suppressed inside the scope");
                scope.TraceParent.Should().Be(expected);
            }

            Activity.Current.Should().Be(act, "the ambient activity is restored on dispose");
        }

        [Test]
        public void SuppressForLinkingHasNullTraceParentWhenNoAmbientActivity()
        {
            Activity.Current.Should().BeNull();
            using var scope = Tracing.SuppressForLinking();
            scope.TraceParent.Should().BeNull();
        }

        [Test]
        public void AddTraceLinkAttachesLinkFromTraceParent()
        {
            using var sender = Tracing.StartActivity("sender");
            var traceParent = sender!.Id;
            using var consumer = Tracing.StartActivity("consumer");

            consumer!.AddTraceLink(traceParent);

            consumer.Links.Should().ContainSingle(l =>
                l.Context.TraceId == sender.TraceId && l.Context.SpanId == sender.SpanId);
        }

        [Test]
        public void AddTraceLinkIsNullAndGarbageSafe()
        {
            Activity a = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            a.AddTraceLink("00-x-y-01").Should().BeNull();

            using var act = Tracing.StartActivity("noise");
            act!.AddTraceLink("not-a-traceparent");
            act.AddTraceLink(42);
            act.Links.Should().BeEmpty();
        }
    }
}
