using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryReplayTests
    {
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
    }
}
