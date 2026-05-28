using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class ObjectChangesChildTests
    {
        private const string Prefix = "Children.CH1";

        [Test]
        public void CalculateChildAddedEmitsSingleSnapshotEntry()
        {
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "ROOT", ChildName = "Alpha", Price = 1.5m };
            var changes = ObjectChanges.CalculateChild(ChangeState.Added, null, child, Prefix, nameof(TestHistoryChildModel.ParentId));
            changes.Should().ContainKey(Prefix);
            changes.Keys.Should().HaveCount(1);
            changes[Prefix].Old.Should().BeNull();
            changes[Prefix].New.Should().BeOfType<Dictionary<string, object>>();
            var snapshot = (Dictionary<string, object>)changes[Prefix].New;
            snapshot.Should().ContainKey(nameof(TestHistoryChildModel.ChildName));
            snapshot.Should().NotContainKey(nameof(TestHistoryChildModel.ParentId));
        }

        [Test]
        public void CalculateChildDeletedEmitsSingleSnapshotWithOldOnly()
        {
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "ROOT", ChildName = "Alpha", Price = 1.5m };
            var changes = ObjectChanges.CalculateChild(ChangeState.Deleted, child, null, Prefix, nameof(TestHistoryChildModel.ParentId));
            changes[Prefix].New.Should().BeNull();
            changes[Prefix].Old.Should().BeOfType<Dictionary<string, object>>();
        }

        [Test]
        public void CalculateChildModifiedEmitsPerPropertyEntries()
        {
            var oldChild = new TestHistoryChildModel { Id = "CH1", ParentId = "ROOT", ChildName = "Alpha", Price = 1.5m };
            var newChild = new TestHistoryChildModel { Id = "CH1", ParentId = "ROOT", ChildName = "Beta", Price = 1.5m };
            var changes = ObjectChanges.CalculateChild(ChangeState.Modified, oldChild, newChild, Prefix, nameof(TestHistoryChildModel.ParentId));
            changes.Should().ContainKey($"{Prefix}.{nameof(TestHistoryChildModel.ChildName)}");
            changes.Should().NotContainKey($"{Prefix}.{nameof(TestHistoryChildModel.Price)}");
            changes.Should().NotContainKey($"{Prefix}.{nameof(TestHistoryChildModel.ParentId)}");
            changes[$"{Prefix}.{nameof(TestHistoryChildModel.ChildName)}"].Old.Should().Be("Alpha");
            changes[$"{Prefix}.{nameof(TestHistoryChildModel.ChildName)}"].New.Should().Be("Beta");
        }

        [Test]
        public void CalculateChildRejectsInvalidState()
        {
            var child = new TestHistoryChildModel { Id = "CH1", ParentId = "ROOT" };
            var act = () => ObjectChanges.CalculateChild(ChangeState.None, child, child, Prefix);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void MergeFromAppendsKeysAndThrowsOnDuplicate()
        {
            var a = new ObjectChanges { ["A"] = new OldNewValue { New = 1 } };
            var b = new ObjectChanges { ["B"] = new OldNewValue { New = 2 } };
            a.MergeFrom(b);
            a.Should().ContainKeys("A", "B");

            var duplicate = new ObjectChanges { ["A"] = new OldNewValue { New = 9 } };
            var act = () => a.MergeFrom(duplicate);
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void MergeFromNullIsNoOp()
        {
            var changes = new ObjectChanges { ["A"] = new OldNewValue { New = 1 } };
            changes.MergeFrom(null).Should().BeSameAs(changes);
        }
    }
}
