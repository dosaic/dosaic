using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit;

public class ChangeSetTests
{
    private class Model1 : IModel
    {
        public required NanoId Id { get; set; }
    }

    private class Model2 : IModel
    {
        public required NanoId Id { get; set; }
    }

    [Test]
    public void ChangeSetCanBeConvertedToTyped()
    {
        var changeSet = new ChangeSet();
        var mc1 = ModelChange.Create(ChangeState.Added, TestExtensions.GetModel<Model1>(),
            TestExtensions.GetModel<Model1>());
        var mc2 = ModelChange.Create(ChangeState.Added, TestExtensions.GetModel<Model2>(),
            TestExtensions.GetModel<Model2>());
        changeSet.AddRange([mc1, mc2]);
        var typedChangeSets = changeSet.GetTypedChangeSets();
        typedChangeSets.Should().HaveCount(2);
        typedChangeSets.Should().ContainKey(typeof(Model1));
        typedChangeSets.Should().ContainKey(typeof(Model2));
        typedChangeSets[typeof(Model1)].Should().BeOfType<ChangeSet<Model1>>();
        typedChangeSets[typeof(Model2)].Should().BeOfType<ChangeSet<Model2>>();
    }

    [Test]
    public void ObjectChangesIsJsonCompatible()
    {
        var objectChanges = new ObjectChanges
        {
            { "Id", new OldNewValue { New = NanoId.Parse("123")! } },
            { "Null", new OldNewValue() },
            { "Name", new OldNewValue { New = "Name" } },
            { "IsValid", new OldNewValue { New = true } },
            { "IsNotValid", new OldNewValue { New = false } },
            { "Count", new OldNewValue { New = 123 } },
            { "Price", new OldNewValue { New = 123.123 } },
            { "Object", new OldNewValue { New = new { X = 1 } } },
            { "Array", new OldNewValue { New = new object[] { new { Y = 2 } } } },
        };
        var json = objectChanges.ToJson();
        json.Should().NotBeNullOrEmpty();
        var parsed = ObjectChanges.FromJson(json);
        parsed.Should().HaveCount(objectChanges.Count);
        parsed["Id"].New.Should().Be("123");
        parsed["Null"].New.Should().BeNull();
        parsed["Name"].New.Should().Be("Name");
        parsed["IsValid"].New.Should().Be(true);
        parsed["IsNotValid"].New.Should().Be(false);
        parsed["Count"].New.Should().Be(123);
        parsed["Price"].New.Should().Be(123.123);
        (parsed["Object"].New as IDictionary<string, object>)!["x"].Should().Be(1);
        ((parsed["Array"].New as object[])![0] as IDictionary<string, object>)!["y"].Should().Be(2);
    }

    [Test]
    public void ObjectChangesCanBeFiltered()
    {
        var objectChanges = new ObjectChanges { { "1", new OldNewValue() }, { "2", new OldNewValue() }, };
        var objectChanges2 = objectChanges.FilterKeys(x => x == "1");
        objectChanges2.Should().NotBeSameAs(objectChanges);
        objectChanges2.Should().HaveCount(1).And.ContainKey("1");

        objectChanges.Should().HaveCount(2).And.ContainKey("1").And.ContainKey("2");
    }
}
