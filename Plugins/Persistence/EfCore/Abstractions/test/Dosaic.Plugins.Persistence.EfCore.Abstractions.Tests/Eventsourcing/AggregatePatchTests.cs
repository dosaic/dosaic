using System.Text.Json;
using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using EntityFrameworkCore.Testing.Common;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Eventsourcing;

public class AggregatePatchTests
{
    private TestResult _testResult;
    private IDb _db;

    [SetUp]
    public void SetUp()
    {
        _testResult = new TestResult
        {
            Id = "root-1",
            Name = "Test Result",
            UnrelatedId = "unrelated-1",
            TestResultChildren = new List<TestResultChild>
            {
                new()
                {
                    Id = "child-1",
                    Name = "Child 1",
                    TestResultId = "root-1",
                    TestResultChildChildren = new List<TestResultChildChild>
                    {
                        new()
                        {
                            Id = "grandchild-1",
                            Name = "Grandchild 1",
                            Age = 10,
                            TestResultChildId = "child-1"
                        },
                        new()
                        {
                            Id = "grandchild-2",
                            Name = "Grandchild 2",
                            Age = 12,
                            TestResultChildId = "child-1"
                        }
                    }
                },
                new()
                {
                    Id = "child-2",
                    Name = "Child 2",
                    TestResultId = "root-1",
                    TestResultChildChildren = new List<TestResultChildChild>
                    {
                        new()
                        {
                            Id = "grandchild-3",
                            Name = "Grandchild 3",
                            Age = 8,
                            TestResultChildId = "child-2"
                        }
                    }
                }
            },
            TestResultChildOneToOne = new TestResultChildOneToOne
            {
                Id = "one-to-one-1",
                Name = "OneToOne 1",
                Age = 5,
                TestResultId = "root-1"
            }
        };

        _db = Substitute.For<IDb>();
    }

    [Test]
    public void IsAggregateRootMatchesCorrectTypes()
    {
        typeof(TestResult).IsAggregateRoot().Should().BeTrue();
        typeof(TestResultChild).IsAggregateRoot().Should().BeFalse();
        typeof(TestResultOneToOneUnrelated).IsAggregateRoot().Should().BeFalse();
    }

    [Test]
    public void IsAggregateChildMatchesCorrectTypes()
    {
        typeof(TestResultChild).IsAggregateChild().Should().BeTrue();
        typeof(TestResultChildChild).IsAggregateChild().Should().BeTrue();
        typeof(TestResult).IsAggregateChild().Should().BeFalse();
        typeof(TestResultOneToOneUnrelated).IsAggregateChild().Should().BeFalse();
    }

    [Test]
    public void GetAggregateInfoForRootReturnsEmptySegments()
    {
        var info = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResult));

        info.Segments.Should().BeEmpty();
        info.RootEntityType.Should().Be(typeof(TestResult));
        info.AggregateEventType.Should().Be(typeof(TestAggregate));
    }

    [Test]
    public void GetAggregateInfoForDirectChildReturnsOneSegment()
    {
        var info = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultChild));

        info.Segments.Should().HaveCount(1);
        info.RootEntityType.Should().Be(typeof(TestResult));
        info.Segments[0].ParentType.Should().Be(typeof(TestResult));
        info.Segments[0].ChildType.Should().Be(typeof(TestResultChild));
        info.Segments[0].DownwardPropertyName.Should().Be(nameof(TestResult.TestResultChildren));
        info.Segments[0].IsCollection.Should().BeTrue();
        info.Segments[0].FkPropertyName.Should().Be("TestResultId");
    }

    [Test]
    public void GetAggregateInfoForGrandchildReturnsTwoSegments()
    {
        var info = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultChildChild));

        info.Segments.Should().HaveCount(2);
        info.RootEntityType.Should().Be(typeof(TestResult));
        info.Segments[0].ParentType.Should().Be(typeof(TestResult));
        info.Segments[0].ChildType.Should().Be(typeof(TestResultChild));
        info.Segments[0].DownwardPropertyName.Should().Be(nameof(TestResult.TestResultChildren));
        info.Segments[1].ParentType.Should().Be(typeof(TestResultChild));
        info.Segments[1].ChildType.Should().Be(typeof(TestResultChildChild));
        info.Segments[1].DownwardPropertyName.Should().Be(nameof(TestResultChild.TestResultChildChildren));
        info.Segments[1].FkPropertyName.Should().Be("TestResultChildId");
    }

    [Test]
    public void GetAggregateInfoForOneToOneReturnsNonCollectionSegment()
    {
        var info = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultChildOneToOne));

        info.Segments.Should().HaveCount(1);
        info.Segments[0].IsCollection.Should().BeFalse();
        info.Segments[0].DownwardPropertyName.Should().Be(nameof(TestResult.TestResultChildOneToOne));
    }

    [Test]
    public void GetAggregateInfoForUndecoratedTypeThrows()
    {
        var act = () => AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultOneToOneUnrelated));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetAggregateInfoIsCached()
    {
        var info1 = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultChild));
        var info2 = AggregatePatchExtensions.GetAggregateInfo(typeof(TestResultChild));

        info1.Should().BeSameAs(info2);
    }

    [Test]
    public async Task CalculateChangesForRootAddSerializesAllProperties()
    {
        var root = new TestResult { Id = "new-root", Name = "New Root", UnrelatedId = "u-1" };
        SetupQuery<TestResult>();

        var patch = await _db.GetAggregateChangesAsync(root, PatchOperation.Add, CancellationToken.None);

        patch.Operation.Should().Be(PatchOperation.Add);
        patch.AggregateId.Should().Be((NanoId)"new-root");
        patch.Path.Should().BeNull();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch.Data);
        data.Should().ContainKey("Name");
        data.Should().ContainKey("UnrelatedId");
    }

    [Test]
    public async Task CalculateChangesForRootUpdateReturnsOnlyChangedFields()
    {
        var existing = new TestResult { Id = "root-1", Name = "Old Name", UnrelatedId = "u-1" };
        var change = new TestResult { Id = "root-1", Name = "New Name", UnrelatedId = "u-1" };
        SetupQuery(existing);

        var patch = await _db.GetAggregateChangesAsync(change, PatchOperation.Update, CancellationToken.None);

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch.Data);
        data.Should().ContainKey("Name");
        data["Name"].GetString().Should().Be("New Name");
        data.Should().NotContainKey("UnrelatedId");
    }

    [Test]
    public async Task CalculateChangesForRootDeleteReturnsNullData()
    {
        var root = new TestResult { Id = "root-1", Name = "Test" };
        SetupQuery(root);

        var patch = await _db.GetAggregateChangesAsync(root, PatchOperation.Delete, CancellationToken.None);

        patch.Operation.Should().Be(PatchOperation.Delete);
        patch.Data.Should().BeNull();
    }

    [Test]
    public async Task CalculateChangesForRootSetsNullPath()
    {
        var root = new TestResult { Id = "root-1", Name = "Test" };
        SetupQuery<TestResult>();

        var patch = await _db.GetAggregateChangesAsync(root, PatchOperation.Add, CancellationToken.None);

        patch.Path.Should().BeNull();
    }

    [Test]
    public async Task CalculateChangesForRootUsesOwnIdAsAggregateId()
    {
        var root = new TestResult { Id = "root-42", Name = "Test" };
        SetupQuery<TestResult>();

        var patch = await _db.GetAggregateChangesAsync(root, PatchOperation.Add, CancellationToken.None);

        patch.AggregateId.Should().Be((NanoId)"root-42");
    }

    [Test]
    public async Task CalculateChangesForDirectChildResolvesFkAsAggregateId()
    {
        var child = new TestResultChild { Id = "child-new", Name = "New Child", TestResultId = "root-1" };
        SetupQuery<TestResultChild>();

        var patch = await _db.GetAggregateChangesAsync(child, PatchOperation.Add, CancellationToken.None);

        patch.AggregateId.Should().Be((NanoId)"root-1");
    }

    [Test]
    public async Task CalculateChangesForDirectChildSetsCorrectPath()
    {
        var child = new TestResultChild { Id = "child-new", Name = "New Child", TestResultId = "root-1" };
        SetupQuery<TestResultChild>();

        var patch = await _db.GetAggregateChangesAsync(child, PatchOperation.Add, CancellationToken.None);

        patch.Path.Should().Be("TestResultChildren");
    }

    [Test]
    public async Task CalculateChangesForDirectChildUpdateDiffsAgainstDb()
    {
        var existing = new TestResultChild { Id = "child-1", Name = "Old Name", TestResultId = "root-1" };
        var change = new TestResultChild { Id = "child-1", Name = "Updated Name", TestResultId = "root-1" };
        SetupQuery(existing);

        var patch = await _db.GetAggregateChangesAsync(change, PatchOperation.Update, CancellationToken.None);

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch.Data);
        data.Should().ContainKey("Name");
        data["Name"].GetString().Should().Be("Updated Name");
        data.Should().NotContainKey("TestResultId");
    }

    [Test]
    public async Task CalculateChangesForGrandchildResolvesAggregateIdViaDb()
    {
        var parent = new TestResultChild { Id = "child-1", Name = "Child 1", TestResultId = "root-1" };
        var grandchild = new TestResultChildChild
        {
            Id = "gc-new",
            Name = "New Grandchild",
            Age = 5,
            TestResultChildId = "child-1"
        };
        SetupQuery<TestResultChildChild>();
        SetupQuery(parent);

        var patch = await _db.GetAggregateChangesAsync(grandchild, PatchOperation.Add, CancellationToken.None);

        patch.AggregateId.Should().Be((NanoId)"root-1");
    }

    [Test]
    public async Task CalculateChangesForGrandchildSetsFullPath()
    {
        var parent = new TestResultChild { Id = "child-1", Name = "Child 1", TestResultId = "root-1" };
        var grandchild = new TestResultChildChild
        {
            Id = "gc-new",
            Name = "New Grandchild",
            Age = 5,
            TestResultChildId = "child-1"
        };
        SetupQuery<TestResultChildChild>();
        SetupQuery(parent);

        var patch = await _db.GetAggregateChangesAsync(grandchild, PatchOperation.Add, CancellationToken.None);

        patch.Path.Should().Be("TestResultChildren.TestResultChildChildren");
    }

    [Test]
    public async Task CalculateChangesForOneToOneSetsNonCollectionPath()
    {
        var oneToOne = new TestResultChildOneToOne
        {
            Id = "oto-new",
            Name = "New OneToOne",
            Age = 3,
            TestResultId = "root-1"
        };
        SetupQuery<TestResultChildOneToOne>();

        var patch = await _db.GetAggregateChangesAsync(oneToOne, PatchOperation.Add, CancellationToken.None);

        patch.Path.Should().Be("TestResultChildOneToOne");
    }

    [Test]
    public async Task CalculateChangesSetsEntityIdAndType()
    {
        var child = new TestResultChild { Id = "child-99", Name = "Test", TestResultId = "root-1" };
        SetupQuery<TestResultChild>();

        var patch = await _db.GetAggregateChangesAsync(child, PatchOperation.Add, CancellationToken.None);

        patch.EntityId.Should().Be((NanoId)"child-99");
        patch.EntityType.Should().Be(typeof(TestResultChild).AssemblyQualifiedName);
    }

    [Test]
    public async Task CalculateChangesSetsCorrectOperation()
    {
        var child = new TestResultChild { Id = "child-1", Name = "Test", TestResultId = "root-1" };
        SetupQuery(child);

        var patchDelete = await _db.GetAggregateChangesAsync(child, PatchOperation.Delete, CancellationToken.None);

        patchDelete.Operation.Should().Be(PatchOperation.Delete);
    }

    [Test]
    public void ApplyUpdateOnRootSetsChangedProperties()
    {
        var patch = new AggregatePatch(
            "root-1", null, PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "Updated Root" }),
            "root-1", typeof(TestResult).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.Name.Should().Be("Updated Root");
    }

    [Test]
    public void ApplyAddOnRootThrows()
    {
        var patch = new AggregatePatch(
            "root-1", null, PatchOperation.Add,
            "{}", "root-1", typeof(TestResult).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ApplyOnNonRootEntityThrows()
    {
        var child = new TestResultChild { Id = "child-1", Name = "Test", TestResultId = "root-1" };
        var patch = new AggregatePatch(
            "root-1", null, PatchOperation.Update,
            "{}", "child-1", typeof(TestResultChild).AssemblyQualifiedName);

        var act = () => child.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ApplyAddDirectChildAddsToCollection()
    {
        var newChild = new TestResultChild { Id = "child-new", Name = "New Child", TestResultId = "root-1" };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Add,
            JsonSerializer.Serialize(newChild),
            "child-new", typeof(TestResultChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildren.Should().HaveCount(3);
        _testResult.TestResultChildren.Should().Contain(c => c.Id == (NanoId)"child-new");
    }

    [Test]
    public void ApplyUpdateDirectChildPatchesMatchingChild()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "Updated Child 1" }),
            "child-1", typeof(TestResultChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-1").Name.Should().Be("Updated Child 1");
    }

    [Test]
    public void ApplyDeleteDirectChildRemovesFromCollection()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Delete,
            null, "child-2", typeof(TestResultChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildren.Should().HaveCount(1);
        _testResult.TestResultChildren.Should().NotContain(c => c.Id == (NanoId)"child-2");
    }

    [Test]
    public void ApplyAddGrandchildAddsToNestedCollection()
    {
        var newGc = new TestResultChildChild
        {
            Id = "gc-new",
            Name = "New GC",
            Age = 99,
            TestResultChildId = "child-1"
        };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Add,
            JsonSerializer.Serialize(newGc),
            "gc-new", typeof(TestResultChildChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        var parent = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-1");
        parent.TestResultChildChildren.Should().HaveCount(3);
        parent.TestResultChildChildren.Should().Contain(gc => gc.Id == (NanoId)"gc-new");
    }

    [Test]
    public void ApplyUpdateGrandchildPatchesNestedChild()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "Updated GC", ["Age"] = 42 }),
            "grandchild-1", typeof(TestResultChildChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        var parent = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-1");
        var gc = parent.TestResultChildChildren.First(g => g.Id == (NanoId)"grandchild-1");
        gc.Name.Should().Be("Updated GC");
        gc.Age.Should().Be(42);
    }

    [Test]
    public void ApplyDeleteGrandchildRemovesFromNestedCollection()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Delete,
            null, "grandchild-2", typeof(TestResultChildChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        var parent = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-1");
        parent.TestResultChildChildren.Should().HaveCount(1);
        parent.TestResultChildChildren.Should().NotContain(gc => gc.Id == (NanoId)"grandchild-2");
    }

    [Test]
    public void ApplyAddOneToOneSetsReference()
    {
        _testResult.TestResultChildOneToOne = null;
        var newOto = new TestResultChildOneToOne
        {
            Id = "oto-new",
            Name = "New OTO",
            Age = 7,
            TestResultId = "root-1"
        };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildOneToOne", PatchOperation.Add,
            JsonSerializer.Serialize(newOto),
            "oto-new", typeof(TestResultChildOneToOne).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildOneToOne.Should().NotBeNull();
        _testResult.TestResultChildOneToOne.Id.Should().Be((NanoId)"oto-new");
    }

    [Test]
    public void ApplyUpdateOneToOnePatchesReference()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildOneToOne", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "Updated OTO" }),
            "one-to-one-1", typeof(TestResultChildOneToOne).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildOneToOne.Name.Should().Be("Updated OTO");
    }

    [Test]
    public void ApplyDeleteOneToOneNullsReference()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildOneToOne", PatchOperation.Delete,
            null, "one-to-one-1", typeof(TestResultChildOneToOne).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildOneToOne.Should().BeNull();
    }

    [Test]
    public async Task TheEntireChainWorksInComplete()
    {
        var child = new TestResultChild { Id = _testResult.TestResultChildren.First().Id, Name = "Something new", TestResultId = _testResult.Id };
        SetupQuery(_testResult);
        SetupQuery(_testResult.TestResultChildren.First());
        var changes = await _db.GetAggregateChangesAsync(child, PatchOperation.Update, CancellationToken.None);
        _testResult.TestResultChildren.First().Name.Should().NotBe("Something new");
        _testResult.ApplyAggregateChanges(changes);
        _testResult.TestResultChildren.First().Name.Should().Be("Something new");
    }

    [Test]
    public async Task TheEntireChainCanHandleOwnedTypes()
    {
        var child = new TestResult { Id = _testResult.Id, Name = "Updated", UnrelatedId = _testResult.UnrelatedId, Owned = new OwnedEnt { Value = ["A", "B"] } };
        SetupQuery(_testResult);
        var changes = await _db.GetAggregateChangesAsync(child, PatchOperation.Update, CancellationToken.None);
        _testResult.Name.Should().NotBe("Updated");
        _testResult.Owned.Should().BeNull();
        _testResult.ApplyAggregateChanges(changes);
        _testResult.Name.Should().Be("Updated");
        _testResult.Owned.Should().NotBeNull();
        _testResult.Owned.Value.Should().HaveCount(2);
        _testResult.Owned.Value.Should().Contain("A");
        _testResult.Owned.Value.Should().Contain("B");
    }

    [Test]
    public async Task TheEntireChainCanHandleOwnedTypesForUpdate()
    {
        _testResult.Owned = new OwnedEnt { Name = "Old", Value = ["X"] };
        var child = new TestResult { Id = _testResult.Id, Name = _testResult.Name, UnrelatedId = _testResult.UnrelatedId, Owned = new OwnedEnt { Value = ["A", "B"] } };
        SetupQuery(_testResult);
        var changes = await _db.GetAggregateChangesAsync(child, PatchOperation.Update, CancellationToken.None);
        _testResult.ApplyAggregateChanges(changes);
        _testResult.Owned.Should().NotBeNull();
        _testResult.Owned.Value.Should().HaveCount(2);
        _testResult.Owned.Value.Should().Contain("A");
        _testResult.Owned.Value.Should().Contain("B");
        _testResult.Owned.Name.Should().BeNull();
    }

    [Test]
    public void ApplyDeleteOnRootThrows()
    {
        var patch = new AggregatePatch(
            "root-1", null, PatchOperation.Delete,
            null, "root-1", typeof(TestResult).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot delete the aggregate root via Apply.");
    }

    [Test]
    public void ApplyToChildWithUnresolvableEntityTypeThrows()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Update,
            "{}", "child-1", "Some.Non.Existent.Type, FakeAssembly");

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not resolve type*");
    }

    [Test]
    public void ApplyUpdateChildWithNonExistentEntityThrows()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "X" }),
            "non-existent-id", typeof(TestResultChild).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Test]
    public void ApplyAddChildCreatesCollectionWhenNull()
    {
        _testResult.TestResultChildren = null;
        var newChild = new TestResultChild { Id = "child-new", Name = "New", TestResultId = "root-1" };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Add,
            JsonSerializer.Serialize(newChild),
            "child-new", typeof(TestResultChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        _testResult.TestResultChildren.Should().HaveCount(1);
        _testResult.TestResultChildren.First().Id.Should().Be((NanoId)"child-new");
    }

    [Test]
    public async Task CalculateChangesUpdateForMissingEntityThrows()
    {
        var change = new TestResult { Id = "missing-id", Name = "X" };
        SetupQuery<TestResult>();

        var act = () => _db.GetAggregateChangesAsync(change, PatchOperation.Update, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found in database*");
    }

    [Test]
    public void ApplyUpdateWithNullDeserializedDataDoesNotThrow()
    {
        var patch = new AggregatePatch(
            "root-1", null, PatchOperation.Update,
            "null", "root-1", typeof(TestResult).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().NotThrow();
    }

    [Test]
    public void ApplyUpdateOneToOneWithWrongIdThrows()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildOneToOne", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "X" }),
            "wrong-id", typeof(TestResultChildOneToOne).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Test]
    public async Task CalculateChangesAddSkipsNullPropertyValues()
    {
        var root = new TestResult { Id = "new-root", Name = null, UnrelatedId = "u-1" };
        SetupQuery<TestResult>();

        var patch = await _db.GetAggregateChangesAsync(root, PatchOperation.Add, CancellationToken.None);

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patch.Data);
        data.Should().NotContainKey("Name");
        data.Should().ContainKey("UnrelatedId");
    }

    [Test]
    public void ApplyDeleteGrandchildFromWrongParentThrows()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Delete,
            null, "non-existent-gc", typeof(TestResultChildChild).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found in collection*");
    }

    [Test]
    public void ApplyUpdateGrandchildWithNonExistentIdThrows()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "X" }),
            "non-existent-gc", typeof(TestResultChildChild).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Test]
    public void ApplyDeleteGrandchildSkipsChildWithNullCollection()
    {
        _testResult.TestResultChildren.First().TestResultChildChildren = null;
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Delete,
            null, "grandchild-3", typeof(TestResultChildChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        var child2 = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-2");
        child2.TestResultChildChildren.Should().BeEmpty();
    }

    [Test]
    public void ApplyAddGrandchildCreatesCollectionWhenNull()
    {
        _testResult.TestResultChildren.First().TestResultChildChildren = null;
        var newGc = new TestResultChildChild
        {
            Id = "gc-new",
            Name = "New GC",
            Age = 1,
            TestResultChildId = "child-1"
        };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Add,
            JsonSerializer.Serialize(newGc),
            "gc-new", typeof(TestResultChildChild).AssemblyQualifiedName);

        _testResult.ApplyAggregateChanges(patch);

        var child1 = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-1");
        child1.TestResultChildChildren.Should().HaveCount(1);
        child1.TestResultChildChildren.First().Id.Should().Be((NanoId)"gc-new");
    }

    [Test]
    public void ApplyAddGrandchildWithNonExistentParentThrows()
    {
        var newGc = new TestResultChildChild
        {
            Id = "gc-new",
            Name = "New GC",
            Age = 1,
            TestResultChildId = "non-existent"
        };
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren.TestResultChildChildren", PatchOperation.Add,
            JsonSerializer.Serialize(newGc),
            "gc-new", typeof(TestResultChildChild).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Test]
    public void ApplyDeleteOneToOneWhenAlreadyNullDoesNotThrow()
    {
        _testResult.TestResultChildOneToOne = null;
        var patch = new AggregatePatch(
            "root-1", "TestResultChildOneToOne", PatchOperation.Delete,
            null, "one-to-one-1", typeof(TestResultChildOneToOne).AssemblyQualifiedName);

        var act = () => _testResult.ApplyAggregateChanges(patch);

        act.Should().NotThrow();
    }

    [Test]
    public void BuildAggregateInfoWithBadNavPropertyThrows()
    {
        var act = () => AggregatePatchExtensions.BuildAggregateInfo(typeof(BadNavChild));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Navigation property*not found*");
    }

    [Test]
    public void BuildAggregateInfoWithNoDownwardPropertyThrows()
    {
        var act = () => AggregatePatchExtensions.BuildAggregateInfo(typeof(OrphanChild));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No property on*references*");
    }

    [Test]
    public async Task CalculateChangesForChildWithMissingFkThrows()
    {
        var child = new NoFkChild { Id = "c-1" };
        SetupQuery<NoFkChild>();

        var act = () => _db.GetAggregateChangesAsync(child, PatchOperation.Add, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FK property*not found*");
    }

    [Test]
    public void ApplyAddDeepGrandchildWithMissingFkThrows()
    {
        var root = new DeepNoFkRoot
        {
            Id = "r-1",
            DeepNoFkChildren = new List<DeepNoFkChild>
            {
                new() { Id = "c-1", DeepNoFkRootId = "r-1" }
            }
        };
        var patch = new AggregatePatch(
            "r-1", "DeepNoFkChildren.DeepNoFkGrandchildren", PatchOperation.Add,
            JsonSerializer.Serialize(new { Id = "gc-1" }),
            "gc-1", typeof(DeepNoFkGrandchild).AssemblyQualifiedName);

        var act = () => root.ApplyAggregateChanges(patch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FK property*not found*");
    }

    [Test]
    public void CanSerializeAndDeserialize()
    {
        var patch = new AggregatePatch(
            "root-1", "TestResultChildren", PatchOperation.Update,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["Name"] = "Updated Child" }),
            "child-1", typeof(TestResultChild).AssemblyQualifiedName);

        var json = patch.ToJson();
        var deserialized = AggregatePatch.FromJson(json);

        deserialized.Should().NotBeNull();
        deserialized!.AggregateId.Should().Be(patch.AggregateId);
        deserialized.Path.Should().Be(patch.Path);
        deserialized.Operation.Should().Be(patch.Operation);
        deserialized.Data.Should().Be(patch.Data);
        deserialized.EntityId.Should().Be(patch.EntityId);
        deserialized.EntityType.Should().Be(patch.EntityType);
    }

    [Test]
    public async Task FullLifecycleAddUpdateDeleteForChildEntity()
    {
        var newChild = new TestResultChild { Id = "child-lifecycle", Name = "Created", TestResultId = "root-1" };
        SetupQuery<TestResultChild>();

        var addPatch = await _db.GetAggregateChangesAsync(newChild, PatchOperation.Add, CancellationToken.None);
        _testResult.ApplyAggregateChanges(addPatch);

        _testResult.TestResultChildren.Should().HaveCount(3);
        var added = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-lifecycle");
        added.Name.Should().Be("Created");
        added.Id.Should().Be((NanoId)"child-lifecycle");
        added.TestResultId.Should().Be((NanoId)"root-1");

        var updatedChild = new TestResultChild { Id = "child-lifecycle", Name = "Updated", TestResultId = "root-1" };
        SetupQuery(newChild);
        var updatePatch = await _db.GetAggregateChangesAsync(updatedChild, PatchOperation.Update, CancellationToken.None);
        _testResult.ApplyAggregateChanges(updatePatch);

        _testResult.TestResultChildren.Should().HaveCount(3);
        var updated = _testResult.TestResultChildren.First(c => c.Id == (NanoId)"child-lifecycle");
        updated.Name.Should().Be("Updated");

        SetupQuery(updatedChild);
        var deletePatch = await _db.GetAggregateChangesAsync(updatedChild, PatchOperation.Delete, CancellationToken.None);
        _testResult.ApplyAggregateChanges(deletePatch);

        _testResult.TestResultChildren.Should().HaveCount(2);
        _testResult.TestResultChildren.Should().NotContain(c => c.Id == (NanoId)"child-lifecycle");
    }

    private void SetupQuery<T>(params T[] items) where T : class, IModel
    {
        var asyncEnum = new AsyncEnumerable<T>(items.ToList());
        _db.GetQuery<T>().Returns(asyncEnum.AsQueryable());
    }
}

[AggregateRoot<TestAggregate>]
internal class TestResult : IModel
{
    public NanoId Id { get; set; }
    public string Name { get; set; }
    public NanoId UnrelatedId { get; set; }
    public OwnedEnt Owned { get; set; }
    public virtual ICollection<TestResultChild> TestResultChildren { get; set; }
    public virtual TestResultChildOneToOne TestResultChildOneToOne { get; set; }
    public virtual TestResultOneToOneUnrelated Unrelated { get; set; }
}

[AggregateChild<TestAggregate>(nameof(TestResultChild.TestResult))]
internal class TestResultChild : IModel
{
    public NanoId Id { get; set; }
    public NanoId TestResultId { get; set; }
    public string Name { get; set; }
    public virtual TestResult TestResult { get; set; }
    public virtual ICollection<TestResultChildChild> TestResultChildChildren { get; set; }
}

[AggregateChild<TestAggregate>(nameof(TestResultChildChild.TestResultChild))]
internal class TestResultChildChild : IModel
{
    public NanoId Id { get; set; }
    public NanoId TestResultChildId { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public virtual TestResultChild TestResultChild { get; set; }
}

[AggregateChild<TestAggregate>(nameof(TestResultChildOneToOne.TestResult))]
internal class TestResultChildOneToOne : IModel
{
    public NanoId Id { get; set; }
    public NanoId TestResultId { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public virtual TestResult TestResult { get; set; }
}

internal class TestResultOneToOneUnrelated : IModel
{
    public NanoId Id { get; set; }
    public string Description { get; set; }
}

internal class OwnedEnt
{
    public string Name { get; set; }
    public string[] Value { get; set; }
}

[AggregateChild<TestAggregate>("NonExistentNav")]
internal class BadNavChild : IModel
{
    public NanoId Id { get; set; }
}

[AggregateRoot<TestAggregate>]
internal class OrphanParent : IModel
{
    public NanoId Id { get; set; }
}

[AggregateChild<TestAggregate>(nameof(OrphanChild.OrphanParent))]
internal class OrphanChild : IModel
{
    public NanoId Id { get; set; }
    public virtual OrphanParent OrphanParent { get; set; }
}

[AggregateRoot<TestAggregate>]
internal class NoFkParent : IModel
{
    public NanoId Id { get; set; }
    public virtual NoFkChild NoFkChild { get; set; }
}

[AggregateChild<TestAggregate>(nameof(NoFkChild.NoFkParent))]
internal class NoFkChild : IModel
{
    public NanoId Id { get; set; }
    public virtual NoFkParent NoFkParent { get; set; }
}

[AggregateRoot<TestAggregate>]
internal class DeepNoFkRoot : IModel
{
    public NanoId Id { get; set; }
    public virtual ICollection<DeepNoFkChild> DeepNoFkChildren { get; set; }
}

[AggregateChild<TestAggregate>(nameof(DeepNoFkChild.DeepNoFkRoot))]
internal class DeepNoFkChild : IModel
{
    public NanoId Id { get; set; }
    public NanoId DeepNoFkRootId { get; set; }
    public virtual DeepNoFkRoot DeepNoFkRoot { get; set; }
    public virtual ICollection<DeepNoFkGrandchild> DeepNoFkGrandchildren { get; set; }
}

[AggregateChild<TestAggregate>(nameof(DeepNoFkGrandchild.DeepNoFkChild))]
internal class DeepNoFkGrandchild : IModel
{
    public NanoId Id { get; set; }
    public virtual DeepNoFkChild DeepNoFkChild { get; set; }
}
