using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class ObjectChangesTests
    {
        [Test]
        public void CalculateThrowsForInvalidChangeState()
        {
            var model = new TestHistoryModel { Id = "R1", HistoryProperty = "x" };
            var act = () => ObjectChanges.Calculate((ChangeState)999, model, model);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void CalculateChildThrowsForInvalidChangeState()
        {
            var child = new TestHistoryChildModel { Id = "C1", ParentId = "R1" };
            var act = () => ObjectChanges.CalculateChild((ChangeState)999, child, child, "Children.C1");
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void CalculateAddedSkipsNullPropertyValues()
        {
            var entity = new TestHistoryModel { Id = "R1", HistoryProperty = null, Ignored = null };
            var changes = ObjectChanges.Calculate(ChangeState.Added, null, entity);
            changes.Should().NotContainKey(nameof(TestHistoryModel.HistoryProperty));
            changes.Should().NotContainKey(nameof(TestHistoryModel.Ignored));
        }

        [Test]
        public void CalculateDeletedSkipsNullPropertyValues()
        {
            var entity = new TestHistoryModel { Id = "R1", HistoryProperty = null, Ignored = null };
            var changes = ObjectChanges.Calculate(ChangeState.Deleted, entity, null);
            changes.Should().BeEmpty();
        }

        [Test]
        public void CalculateModifiedDetectsAllNullPermutations()
        {
            var oldEntity = new TestHistoryModel { Id = "R1", HistoryProperty = null };
            var newEntity = new TestHistoryModel { Id = "R1", HistoryProperty = "x" };
            var addLike = ObjectChanges.Calculate(ChangeState.Modified, oldEntity, newEntity);
            addLike.Should().ContainKey(nameof(TestHistoryModel.HistoryProperty));
            addLike[nameof(TestHistoryModel.HistoryProperty)].Old.Should().BeNull();
            addLike[nameof(TestHistoryModel.HistoryProperty)].New.Should().Be("x");

            var removeLike = ObjectChanges.Calculate(ChangeState.Modified, newEntity, oldEntity);
            removeLike[nameof(TestHistoryModel.HistoryProperty)].Old.Should().Be("x");
            removeLike[nameof(TestHistoryModel.HistoryProperty)].New.Should().BeNull();

            var bothNull = ObjectChanges.Calculate(ChangeState.Modified, oldEntity, oldEntity);
            bothNull.Should().NotContainKey(nameof(TestHistoryModel.HistoryProperty));
        }

        [Test]
        public void FromJsonHandlesNullJsonElementAsNull()
        {
            var json = "{\"Key\":{\"Old\":null,\"New\":null}}";
            var changes = ObjectChanges.FromJson(json);
            changes.Should().ContainKey("Key");
            changes["Key"].Old.Should().BeNull();
            changes["Key"].New.Should().BeNull();
        }

        [Test]
        public void FromJsonHandlesUnknownJsonValueKindWithinArrayAsNull()
        {
            // The JSON null element ends up in the GetCleanValue switch as JsonValueKind.Null
            // and falls through the unknown-kind arm returning null.
            var json = "{\"Key\":{\"Old\":null,\"New\":[null,\"a\"]}}";
            var changes = ObjectChanges.FromJson(json);
            var arr = changes["Key"].New.Should().BeOfType<object[]>().Subject;
            arr.Should().HaveCount(2);
            arr[0].Should().BeNull();
            arr[1].Should().Be("a");
        }

        [Test]
        public void FromJsonRoundtripsScalarPrimitives()
        {
            var json = "{\"S\":{\"New\":\"hi\"},\"I\":{\"New\":7},\"D\":{\"New\":1.5},\"T\":{\"New\":true},\"F\":{\"New\":false},\"O\":{\"New\":{\"Inner\":\"v\"}},\"W\":{\"New\":{\"value\":\"unwrapped\"}}}";
            var changes = ObjectChanges.FromJson(json);
            changes["S"].New.Should().Be("hi");
            changes["I"].New.Should().Be(7L);
            changes["D"].New.Should().Be(1.5m);
            changes["T"].New.Should().Be(true);
            changes["F"].New.Should().Be(false);
            changes["O"].New.Should().BeOfType<Dictionary<string, object>>();
            changes["W"].New.Should().Be("unwrapped");
        }
    }
}
