using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryReplayTests
    {
        public class ReplayAddress
        {
            public string Street { get; set; }
        }

        public class StringBag : List<string>
        {
        }

        [DbNanoIdPrimaryKey(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
        public class ReplayTargetModel : Model, IHistory
        {
            public int IntScalar { get; set; }
            public TestEnumType EnumProp { get; set; }
            public TestEnumType EnumFromNumeric { get; set; }
            public Guid GuidProp { get; set; }
            public DateTime DateTimeProp { get; set; }
            public DateTimeOffset DateTimeOffsetProp { get; set; }
            public ReplayAddress Address { get; set; }
            public ReplayAddress Address2 { get; set; }
            public int[] IntArray { get; set; }
            public List<string> StringList { get; set; }
            public StringBag Bag { get; set; }
            public Uri UriProp { get; set; }
        }

        private static ObjectChanges Roundtrip(ObjectChanges changes) =>
            ObjectChanges.FromJson(changes.ToJson());

        [Test]
        public void ReplayReturnsNullWhenNoRows()
        {
            HistoryReplay.Replay<TestHistoryModel>("R1", Array.Empty<ObjectChanges>())
                .Should().BeNull();
        }

        [Test]
        public void ReplayRootScalarChanges()
        {
            NanoId rootId = "R1";
            var add = ObjectChanges.Calculate(ChangeState.Added, null,
                new TestHistoryModel { Id = rootId, HistoryProperty = "Alpha" });
            var update = ObjectChanges.Calculate(ChangeState.Modified,
                new TestHistoryModel { Id = rootId, HistoryProperty = "Alpha" },
                new TestHistoryModel { Id = rootId, HistoryProperty = "Beta" });
            var result = HistoryReplay.Replay<TestHistoryModel>(rootId,
                new[] { Roundtrip(add), Roundtrip(update) });
            result.Should().NotBeNull();
            result.Id.Should().Be(rootId);
            result.HistoryProperty.Should().Be("Beta");
        }

        [Test]
        public void ReplayAddRemoveChildElement()
        {
            NanoId rootId = "R1";
            var rootAdd = ObjectChanges.Calculate(ChangeState.Added, null,
                new TestHistoryModel { Id = rootId, HistoryProperty = "Alpha" });
            var childAdd = ObjectChanges.CalculateChild(ChangeState.Added, null,
                new TestHistoryChildModel { Id = "CH1", ParentId = rootId, ChildName = "Item", Price = 1m },
                "Children.CH1", nameof(TestHistoryChildModel.ParentId));
            var childRemove = ObjectChanges.CalculateChild(ChangeState.Deleted,
                new TestHistoryChildModel { Id = "CH1", ParentId = rootId, ChildName = "Item", Price = 1m },
                null, "Children.CH1", nameof(TestHistoryChildModel.ParentId));

            var afterAdd = HistoryReplay.Replay<TestHistoryModel>(rootId,
                new[] { Roundtrip(Merge(rootAdd, childAdd)) });
            afterAdd.Children.Should().HaveCount(1);
            afterAdd.Children.Single().Id.Should().Be((NanoId)"CH1");
            afterAdd.Children.Single().ChildName.Should().Be("Item");

            var afterRemove = HistoryReplay.Replay<TestHistoryModel>(rootId,
                new[] { Roundtrip(Merge(rootAdd, childAdd)), Roundtrip(childRemove) });
            afterRemove.Children.Should().BeEmpty();
        }

        [Test]
        public void ReplayModifyDeepNestedScalar()
        {
            NanoId rootId = "R1";
            var rootAdd = ObjectChanges.Calculate(ChangeState.Added, null,
                new TestHistoryModel { Id = rootId, HistoryProperty = "Alpha" });
            var childAdd = ObjectChanges.CalculateChild(ChangeState.Added, null,
                new TestHistoryChildModel { Id = "CH1", ParentId = rootId, ChildName = "Item", Price = 1m },
                "Children.CH1", nameof(TestHistoryChildModel.ParentId));
            var grandAdd = ObjectChanges.CalculateChild(ChangeState.Added, null,
                new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "PartA", Qty = 5 },
                "Children.CH1.Parts.G1", nameof(TestHistoryGrandchildModel.ChildId));
            var grandModify = ObjectChanges.CalculateChild(ChangeState.Modified,
                new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "PartA", Qty = 5 },
                new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "PartA", Qty = 9 },
                "Children.CH1.Parts.G1", nameof(TestHistoryGrandchildModel.ChildId));

            var result = HistoryReplay.Replay<TestHistoryModel>(rootId, new[]
            {
                Roundtrip(Merge(rootAdd, childAdd, grandAdd)),
                Roundtrip(grandModify)
            });
            result.Children.Single().Parts.Single().PartName.Should().Be("PartA");
            result.Children.Single().Parts.Single().Qty.Should().Be(9);
        }

        [Test]
        public void ReplayRemovingTopWithoutSubtreeStillWorks()
        {
            NanoId rootId = "R1";
            var rootAdd = ObjectChanges.Calculate(ChangeState.Added, null,
                new TestHistoryModel { Id = rootId, HistoryProperty = "Alpha" });
            var childAdd = ObjectChanges.CalculateChild(ChangeState.Added, null,
                new TestHistoryChildModel { Id = "CH1", ParentId = rootId, ChildName = "Item", Price = 1m },
                "Children.CH1", nameof(TestHistoryChildModel.ParentId));
            var grandAdd = ObjectChanges.CalculateChild(ChangeState.Added, null,
                new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "PartA", Qty = 5 },
                "Children.CH1.Parts.G1", nameof(TestHistoryGrandchildModel.ChildId));
            var grandRemove = ObjectChanges.CalculateChild(ChangeState.Deleted,
                new TestHistoryGrandchildModel { Id = "G1", ChildId = "CH1", PartName = "PartA", Qty = 5 },
                null, "Children.CH1.Parts.G1", nameof(TestHistoryGrandchildModel.ChildId));
            var childRemove = ObjectChanges.CalculateChild(ChangeState.Deleted,
                new TestHistoryChildModel { Id = "CH1", ParentId = rootId, ChildName = "Item", Price = 1m },
                null, "Children.CH1", nameof(TestHistoryChildModel.ParentId));

            // single bundled save with cascade removes — depth-sort applies leaves first
            var bundle = Merge(grandRemove, childRemove);
            var result = HistoryReplay.Replay<TestHistoryModel>(rootId, new[]
            {
                Roundtrip(Merge(rootAdd, childAdd, grandAdd)),
                Roundtrip(bundle)
            });
            result.Children.Should().BeEmpty();
        }

        [Test]
        public void ReplayThrowsOnUnknownProperty()
        {
            NanoId rootId = "R1";
            var bogus = new ObjectChanges { ["NonExistent"] = new OldNewValue { New = "x" } };
            var act = () => HistoryReplay.Replay<TestHistoryModel>(rootId, new[] { bogus });
            act.Should().Throw<InvalidOperationException>();
        }

        private static ObjectChanges Merge(params ObjectChanges[] sets)
        {
            var result = new ObjectChanges();
            foreach (var s in sets) result.MergeFrom(s);
            return result;
        }

        [Test]
        public void ReplayThrowsWhenCollectionPathHasNoIdSegment()
        {
            var bogus = new ObjectChanges { ["Children"] = new OldNewValue { New = "x" } };
            var act = () => HistoryReplay.Replay<TestHistoryModel>("R1", new[] { bogus });
            act.Should().Throw<InvalidOperationException>().WithMessage("*ends on collection 'Children' without an id*");
        }

        [Test]
        public void ReplayThrowsWhenNestedCollectionElementMissing()
        {
            var bogus = new ObjectChanges
            {
                ["Children.MISSING.Parts.PX"] = new OldNewValue { New = "x" }
            };
            var act = () => HistoryReplay.Replay<TestHistoryModel>("R1", new[] { bogus });
            act.Should().Throw<InvalidOperationException>().WithMessage("*could not locate element 'MISSING' in collection 'Children'*");
        }

        [Test]
        public void ReplayNonNullableValueScalarSetToNullFallsBackToDefault()
        {
            var changes = new ObjectChanges
            {
                ["IntScalar"] = new OldNewValue { Old = 5, New = null }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.IntScalar.Should().Be(0);
        }

        [Test]
        public void ReplayNestedNavigationInitializesNullSubObject()
        {
            var changes = new ObjectChanges
            {
                ["Address.Street"] = new OldNewValue { New = "Main" }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.Address.Should().NotBeNull();
            result.Address.Street.Should().Be("Main");
        }

        [Test]
        public void ReplayCoercesEnumFromStringAndFromNumeric()
        {
            var changes = new ObjectChanges
            {
                ["EnumProp"] = new OldNewValue { New = "Value2" },
                ["EnumFromNumeric"] = new OldNewValue { New = 1 }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.EnumProp.Should().Be(TestEnumType.Value2);
            result.EnumFromNumeric.Should().Be(TestEnumType.Value2);
        }

        [Test]
        public void ReplayCoercesGuidDateTimeAndDateTimeOffsetFromString()
        {
            var guid = "11111111-1111-1111-1111-111111111111";
            var dt = "2024-05-06T07:08:09Z";
            var dto = "2024-05-06T07:08:09+02:00";
            var changes = new ObjectChanges
            {
                ["GuidProp"] = new OldNewValue { New = guid },
                ["DateTimeProp"] = new OldNewValue { New = dt },
                ["DateTimeOffsetProp"] = new OldNewValue { New = dto }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.GuidProp.Should().Be(Guid.Parse(guid));
            result.DateTimeProp.Should().Be(DateTime.Parse(dt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
            result.DateTimeOffsetProp.Should().Be(DateTimeOffset.Parse(dto, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
        }

        [Test]
        public void ReplayCoercesDictionarySnapshotIntoComplexProperty()
        {
            var dict = new Dictionary<string, object> { ["Street"] = "Elm" };
            var changes = new ObjectChanges
            {
                ["Address2"] = new OldNewValue { New = dict }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.Address2.Should().NotBeNull();
            result.Address2.Street.Should().Be("Elm");
        }

        [Test]
        public void ReplayCoercesObjectArrayIntoTypedArrayAndList()
        {
            var changes = new ObjectChanges
            {
                ["IntArray"] = new OldNewValue { New = new object[] { 1, 2, 3 } },
                ["StringList"] = new OldNewValue { New = new object[] { "a", "b" } }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.IntArray.Should().BeEquivalentTo(new[] { 1, 2, 3 });
            result.StringList.Should().BeEquivalentTo(new[] { "a", "b" });
        }

        [Test]
        public void ReplayConvertChangeTypeFailureFallsBackToRawValue()
        {
            // Convert.ChangeType from int to Uri throws -> CoerceValue catch returns raw int ->
            // PropertyInfo.SetValue rejects it. We only need the catch branch executed; the
            // resulting reflection-level exception is acceptable evidence the path was taken.
            var changes = new ObjectChanges
            {
                ["UriProp"] = new OldNewValue { New = 42 }
            };
            var act = () => HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            act.Should().Throw<Exception>();
        }

        [Test]
        public void ReplayIsEntityCollectionUsesIEnumerableInterfaceFallbackForCustomCollection()
        {
            // StringBag : List<string> exposes IEnumerable<string> only via interface inheritance and
            // is itself non-generic. The path "Bag" is a terminal scalar (string element != IModel)
            // and exercises the IEnumerable<>-interface fallback inside GetCollectionElementType.
            var changes = new ObjectChanges
            {
                ["Bag"] = new OldNewValue { New = new StringBag { "x", "y" } }
            };
            var result = HistoryReplay.Replay<ReplayTargetModel>("R1", new[] { changes });
            result.Bag.Should().BeEquivalentTo(new[] { "x", "y" });
        }
    }
}
