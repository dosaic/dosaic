using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit;

public class ModelChangeTests
{
    [Test]
    public void TypedModelChangeCanCalculateChanges()
    {
        var m1 = TestExtensions.GetModel<TestModel>(x => x.Name = "New");
        var m2 = TestExtensions.GetModel<TestModel>(x => x.Name = "Old");
        var mc = new ModelChange<TestModel>(ChangeState.Modified, m1, m2);
        mc.State.Should().Be(ChangeState.Modified);
        mc.Entity.Should().Be(m1);
        mc.PreviousEntity.Should().Be(m2);
        var changes = mc.GetChanges();
        changes.Should().HaveCount(1);
        changes.Should().ContainKey(nameof(TestModel.Name));
        var nameChange = changes[nameof(TestModel.Name)];
        nameChange.Old.Should().Be(m2.Name);
        nameChange.New.Should().Be(m1.Name);
    }
}
