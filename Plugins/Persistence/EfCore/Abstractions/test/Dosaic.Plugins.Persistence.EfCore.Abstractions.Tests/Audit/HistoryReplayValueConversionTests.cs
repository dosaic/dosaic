using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryReplayValueConversionTests
    {
        public enum Target { Root, ChildSnapshot, ChildProperty }

        public enum ReplayKind { First, Second }

        public abstract class ValueHost : Model
        {
            public string Name { get; set; }
            public DateOnly Day { get; set; }
            public DateOnly? OptionalDay { get; set; }
            public TimeOnly At { get; set; }
            public TimeOnly? OptionalAt { get; set; }
            public DateTime Stamp { get; set; }
            public DateTimeOffset StampOffset { get; set; }
            public Guid Reference { get; set; }
            public ReplayKind Kind { get; set; }
            public int Count { get; set; }
            public decimal Amount { get; set; }
            public NanoId OtherId { get; set; }
        }

        public class ValueRoot : ValueHost, IHistory
        {
            public ICollection<ValueChild> Items { get; set; }
        }

        public class ValueChild : ValueHost;

        private static ValueHost ReplayValue(Target target, string property, object value,
            HistoryReplayOptions options = null)
        {
            var create = new ObjectChanges { [nameof(ValueHost.Name)] = new OldNewValue { New = "root" } };
            var rows = new List<ObjectChanges> { create };
            switch (target)
            {
                case Target.Root:
                    rows.Add(new ObjectChanges { [property] = new OldNewValue { Old = "previous", New = value } });
                    break;
                case Target.ChildSnapshot:
                    create["Items.C1"] = new OldNewValue
                    {
                        New = new Dictionary<string, object> { [nameof(ValueHost.Name)] = "child", [property] = value }
                    };
                    break;
                case Target.ChildProperty:
                    create["Items.C1"] = new OldNewValue
                    {
                        New = new Dictionary<string, object> { [nameof(ValueHost.Name)] = "child" }
                    };
                    rows.Add(new ObjectChanges
                    {
                        [$"Items.C1.{property}"] = new OldNewValue { Old = "previous", New = value }
                    });
                    break;
            }

            var root = HistoryReplay.Replay<ValueRoot>("R1", rows.Select(r => ObjectChanges.FromJson(r.ToJson())),
                options);
            return target == Target.Root ? root : root.Items.Single();
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void ConvertsIsoDateStringToDateOnly(Target target)
        {
            ReplayValue(target, nameof(ValueHost.Day), "2025-04-01").Day.Should().Be(new DateOnly(2025, 4, 1));
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void ConvertsIsoDateStringToNullableDateOnly(Target target)
        {
            ReplayValue(target, nameof(ValueHost.OptionalDay), "2025-04-01").OptionalDay.Should()
                .Be(new DateOnly(2025, 4, 1));
        }

        [TestCase(Target.Root, "2025-03-31T22:00:00Z")]
        [TestCase(Target.ChildSnapshot, "2025-03-31T22:00:00Z")]
        [TestCase(Target.ChildProperty, "2025-03-31T22:00:00Z")]
        [TestCase(Target.Root, "2025-04-01T00:00:00+02:00")]
        [TestCase(Target.ChildSnapshot, "2025-04-01T00:00:00+02:00")]
        [TestCase(Target.ChildProperty, "2025-04-01T00:00:00+02:00")]
        public void ConvertsTimestampStringToUtcDateOfDateOnly(Target target, string timestamp)
        {
            var result = ReplayValue(target, nameof(ValueHost.Day), timestamp);
            result.Day.Should().Be(new DateOnly(2025, 3, 31));
            ReplayValue(target, nameof(ValueHost.OptionalDay), timestamp).OptionalDay.Should()
                .Be(new DateOnly(2025, 3, 31));
        }

        [TestCase(Target.Root, "13:45:30", 0)]
        [TestCase(Target.ChildSnapshot, "13:45:30", 0)]
        [TestCase(Target.ChildProperty, "13:45:30", 0)]
        [TestCase(Target.Root, "13:45:30.1234567", 1234567)]
        [TestCase(Target.ChildSnapshot, "13:45:30.1234567", 1234567)]
        [TestCase(Target.ChildProperty, "13:45:30.1234567", 1234567)]
        public void ConvertsTimeStringToTimeOnly(Target target, string value, long fractionTicks)
        {
            var expected = new TimeOnly(13, 45, 30).Add(TimeSpan.FromTicks(fractionTicks));
            ReplayValue(target, nameof(ValueHost.At), value).At.Should().Be(expected);
            ReplayValue(target, nameof(ValueHost.OptionalAt), value).OptionalAt.Should().Be(expected);
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void NullValuesResetToNullOrDefault(Target target)
        {
            ReplayValue(target, nameof(ValueHost.OptionalDay), null).OptionalDay.Should().BeNull();
            ReplayValue(target, nameof(ValueHost.OptionalAt), null).OptionalAt.Should().BeNull();
            ReplayValue(target, nameof(ValueHost.Day), null).Day.Should().Be(default);
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void CustomConverterOverridesBuiltInRule(Target target)
        {
            var seen = new List<PropertyInfo>();
            var options = new HistoryReplayOptions
            {
                ValueConverter = (property, value) =>
                {
                    seen.Add(property);
                    var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (type == typeof(DateOnly) && value is string s && s.Contains('T'))
                    {
                        var local = DateTimeOffset.Parse(s, CultureInfo.InvariantCulture)
                            .ToOffset(TimeSpan.FromHours(2));
                        return DateOnly.FromDateTime(local.DateTime);
                    }
                    return value;
                }
            };

            var result = ReplayValue(target, nameof(ValueHost.Day), "2025-03-31T22:00:00Z", options);

            result.Day.Should().Be(new DateOnly(2025, 4, 1));
            result.Name.Should().Be(target == Target.Root ? "root" : "child");
            seen.Should().Contain(p => p.Name == nameof(ValueHost.Day));
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void CustomConverterReturningInputFallsBackToBuiltInConversion(Target target)
        {
            var options = new HistoryReplayOptions { ValueConverter = (_, value) => value };
            ReplayValue(target, nameof(ValueHost.Day), "2025-04-01", options).Day.Should()
                .Be(new DateOnly(2025, 4, 1));
        }

        [TestCase(Target.Root)]
        [TestCase(Target.ChildSnapshot)]
        [TestCase(Target.ChildProperty)]
        public void ExistingTypesAreConvertedAsBefore(Target target)
        {
            var guid = Guid.NewGuid();
            var stamp = ReplayValue(target, nameof(ValueHost.Stamp), "2025-03-31T22:00:00Z").Stamp;
            stamp.Should().Be(new DateTime(2025, 3, 31, 22, 0, 0, DateTimeKind.Utc));
            stamp.Kind.Should().Be(DateTimeKind.Utc);
            ReplayValue(target, nameof(ValueHost.Stamp), "2025-03-31T22:00:00").Stamp.Kind.Should()
                .Be(DateTimeKind.Unspecified);
            ReplayValue(target, nameof(ValueHost.StampOffset), "2025-04-01T00:00:00+02:00").StampOffset.Should()
                .Be(new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.FromHours(2)));
            ReplayValue(target, nameof(ValueHost.Reference), guid.ToString()).Reference.Should().Be(guid);
            ReplayValue(target, nameof(ValueHost.Kind), "second").Kind.Should().Be(ReplayKind.Second);
            ReplayValue(target, nameof(ValueHost.Kind), 1).Kind.Should().Be(ReplayKind.Second);
            ReplayValue(target, nameof(ValueHost.Count), 42).Count.Should().Be(42);
            ReplayValue(target, nameof(ValueHost.Amount), 12.5m).Amount.Should().Be(12.5m);
            ReplayValue(target, nameof(ValueHost.OtherId), "X1").OtherId.Should().Be((NanoId)"X1");
            ReplayValue(target, nameof(ValueHost.Name), "renamed").Name.Should().Be("renamed");
        }

        [Test]
        public void ReplaysDateOnlyWrittenByChangeCalculation()
        {
            NanoId rootId = "R1";
            var add = ObjectChanges.Calculate(ChangeState.Added, null,
                new ValueRoot { Id = rootId, Day = new DateOnly(2025, 4, 1), At = new TimeOnly(8, 15) });
            var update = ObjectChanges.Calculate(ChangeState.Modified,
                new ValueRoot { Id = rootId, Day = new DateOnly(2025, 4, 1), At = new TimeOnly(8, 15) },
                new ValueRoot { Id = rootId, Day = new DateOnly(2025, 4, 2), At = new TimeOnly(8, 15), OptionalDay = new DateOnly(2025, 5, 1) });

            var result = HistoryReplay.Replay<ValueRoot>(rootId,
                new[] { ObjectChanges.FromJson(add.ToJson()), ObjectChanges.FromJson(update.ToJson()) });

            result.Day.Should().Be(new DateOnly(2025, 4, 2));
            result.OptionalDay.Should().Be(new DateOnly(2025, 5, 1));
            result.At.Should().Be(new TimeOnly(8, 15));
        }

        [Test]
        public void ConvertValueHandlesNonStringDateAndTimeSources()
        {
            HistoryReplay.ConvertValue(new DateTime(2025, 4, 1, 23, 0, 0, DateTimeKind.Utc), typeof(DateOnly))
                .Should().Be(new DateOnly(2025, 4, 1));
            HistoryReplay.ConvertValue(new DateTimeOffset(2025, 4, 1, 1, 0, 0, TimeSpan.FromHours(2)), typeof(DateOnly?))
                .Should().Be(new DateOnly(2025, 3, 31));
            HistoryReplay.ConvertValue(TimeSpan.FromHours(9), typeof(TimeOnly)).Should().Be(new TimeOnly(9, 0));
            HistoryReplay.ConvertValue(new DateTime(2025, 4, 1, 9, 30, 0, DateTimeKind.Utc), typeof(TimeOnly))
                .Should().Be(new TimeOnly(9, 30));
            HistoryReplay.ConvertValue(new DateTimeOffset(2025, 4, 1, 11, 30, 0, TimeSpan.FromHours(2)), typeof(TimeOnly))
                .Should().Be(new TimeOnly(9, 30));
            HistoryReplay.ConvertValue("2025-04-01T09:30:00Z", typeof(TimeOnly)).Should().Be(new TimeOnly(9, 30));
            HistoryReplay.ConvertValue(new DateOnly(2025, 4, 1), typeof(DateOnly?)).Should().Be(new DateOnly(2025, 4, 1));
            HistoryReplay.ConvertValue(null, typeof(DateOnly?)).Should().BeNull();
            HistoryReplay.ConvertValue(true, typeof(DateOnly)).Should().Be(true);
            HistoryReplay.ConvertValue(true, typeof(TimeOnly)).Should().Be(true);
        }
    }
}
