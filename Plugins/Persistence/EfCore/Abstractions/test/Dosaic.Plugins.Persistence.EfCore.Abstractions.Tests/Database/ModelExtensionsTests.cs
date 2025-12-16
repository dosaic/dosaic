using AwesomeAssertions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    [TestFixture]
    public class ModelExtensionsTests
    {
        [Test]
        public void UnionPropertiesCopiesNonNullValuesFromSourceToTarget()
        {
            var target = new TestModel() { Id = "1", Name = "Old Name" };
            var source = new TestModel { Id = "1", Name = "New Name" };

            target.PatchModel(source);
            target.Name.Should().Be("New Name");
        }

        [Test]
        public void UnionPropertiesDoesNotOverrideWithNullValues()
        {
            var target = new TestModel { Id = "1", Name = "Name" };
            var source = new TestModel { Id = "1", Name = null!, PropertyName = "123" };

            target.PatchModel(source);

            target.Name.Should().Be("Name");
            target.PropertyName.Should().Be("123");
        }

        [Test]
        public void UnionPropertiesAddsNewItemsToLists()
        {
            var grm1 = Activator.CreateInstance<SubTestModel>();
            var grm2 = Activator.CreateInstance<SubTestModel>();
            var target = new TestAuditModel { Id = "1", Name = "test", Subs = [grm1], CreatedBy = "test" };
            var source = new TestAuditModel { Id = "1", Name = "test", Subs = [grm1, grm2], CreatedBy = "test" };

            target.PatchModel(source);
            target.Subs.Should().HaveCount(2);
            target.Subs.Should().Contain([grm1, grm2]);
        }

        [Test]
        public void UnionPropertiesDoesNothingWhenSourceIsNull()
        {
            var target = new TestModel() { Id = "1", Name = "Name" };

            target.PatchModel(null);

            target.Id.Should().Be(NanoId.Parse("1")!.Value);
            target.Name.Should().Be("Name");
        }

        [Test]
        public void UnionPropertiesDoesNothingWhenTargetIsNull()
        {
            TestModel target = null!;
            var source = new TestModel { Id = "1", Name = "Name" };

            // ReSharper disable once ExpressionIsAlwaysNull
            target.PatchModel(source);
            // ReSharper disable once ExpressionIsAlwaysNull
            target.Should().BeNull();
        }

        [Test]
        public void CanPatchModelsWithOwnedObjects()
        {
            var m = new TestModelWithObjectProp { Id = "123", Object = new TestObject { Name = "test" } };
            var m2 = new TestModelWithObjectProp { Id = "123", Object = new TestObject { Name = "test 123" } };
            m.PatchModel(m2);
            m.Object.Name.Should().Be("test 123");
        }

        private class TestModelWithObjectProp : IModel
        {
            public required NanoId Id { get; set; }
            public TestObject Object { get; set; }
        }

        private class TestObject
        {
            public string Name { get; set; } = null!;
        }
    }
}
