using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenTelemetry.Trace;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests
{
    public class HangfireSqlNoiseProcessorTest
    {
        private ActivitySource _source;
        private ActivityListener _listener;

        [SetUp]
        public void Setup()
        {
            _source = new ActivitySource(nameof(HangfireSqlNoiseProcessorTest));
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == nameof(HangfireSqlNoiseProcessorTest),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
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
        public void SuppressesActivitiesWithHangfireSchemaInDbStatement()
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.statement", "SELECT * FROM \"hangfire\".\"job\"");
            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeTrue();

            var processor = new HangfireSqlNoiseProcessor();
            processor.OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeFalse();
        }

        [Test]
        public void KeepsApplicationActivities()
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.statement", "SELECT * FROM \"public\".\"contracts\"");

            new HangfireSqlNoiseProcessor().OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeTrue();
        }

        [Test]
        public void ChecksDbQueryTextTag()
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.query.text", "UPDATE \"hangfire\".\"server\" SET ...");

            new HangfireSqlNoiseProcessor().OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeFalse();
        }

        [Test]
        public void ChecksDbStatementTextTag()
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.statement.text", "DELETE FROM \"hangfire\".\"jobqueue\"");

            new HangfireSqlNoiseProcessor().OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeFalse();
        }

        [TestCase("SELECT * FROM HANGFIRE.STATE")]
        [TestCase("select s.\"name\" from \"Hangfire\".\"state\"")]
        [TestCase("SELECT s.\"name\" \"Name\", s.\"reason\" \"Reason\" "
                  + "FROM \"hangfire\".\"state\" s INNER JOIN \"hangfire\".\"job\" j on j.\"stateid\" = s.\"id\" "
                  + "WHERE j.\"id\" = @JobId;")]
        public void MatchesCaseInsensitiveAndUnquotedSchemas(string sql)
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.query.text", sql);

            new HangfireSqlNoiseProcessor().OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeFalse();
        }

        [Test]
        public void MatchesViaDisplayName()
        {
            using var activity = _source.StartActivity("operation")!;
            activity.DisplayName = "SELECT \"hangfire\".\"job\"";

            new HangfireSqlNoiseProcessor().OnEnd(activity);

            activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded).Should().BeFalse();
        }

        [Test]
        public void MatchesViaOperationName()
        {
            using var activity = _source.StartActivity("hangfire.poll")!;
            activity.DisplayName = "benign.span.name";

            HangfireSqlNoiseProcessor.ShouldSuppress(activity).Should().BeTrue();
        }

        [Test]
        public void IgnoresNonStringStatementTagValues()
        {
            using var activity = _source.StartActivity("db.query")!;
            activity.SetTag("db.statement", 42);

            HangfireSqlNoiseProcessor.ShouldSuppress(activity).Should().BeFalse();
        }

        [Test]
        public void ShouldSuppressReturnsFalseForNullActivity()
        {
            HangfireSqlNoiseProcessor.ShouldSuppress(null!).Should().BeFalse();
        }

        [Test]
        public void RegisterFirstMovesDescriptorToFrontOfServiceCollection()
        {
            var sc = new ServiceCollection();
            sc.ConfigureOpenTelemetryTracerProvider((_, _) => { });
            sc.ConfigureOpenTelemetryTracerProvider((_, _) => { });
            var before = sc.Count;

            HangfireSqlNoiseProcessor.RegisterFirst(sc);

            sc.Count.Should().Be(before + 1);
            sc[0].ServiceType.FullName.Should().Contain("ConfigureTracerProviderBuilder");
        }
    }
}
