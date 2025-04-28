using System.Reflection;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using FluentAssertions;
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

            target.Patch(source);
            target.Name.Should().Be("New Name");
        }

        [Test]
        public void UnionPropertiesDoesNotOverrideWithNullValues()
        {
            var target = new TestModel { Id = "1", Name = "Name" };
            var source = new TestModel { Id = "1", Name = null!, PropertyName = "123" };

            target.Patch(source);

            target.Name.Should().Be("Name");
            target.PropertyName.Should().Be("123");
        }

        [Test]
        public void UnionPropertiesAddsNewItemsToLists()
        {
            var grm1 = Activator.CreateInstance<SubTestModel>();
            var grm2 = Activator.CreateInstance<SubTestModel>();
            var target = new TestAuditModel { Id = "1", Name = "test", Subs = [grm1] };
            var source = new TestAuditModel { Id = "1", Name = "test", Subs = [grm1, grm2] };

            target.Patch(source);
            target.Subs.Should().HaveCount(2);
            target.Subs.Should().Contain([grm1, grm2]);
        }

        [Test]
        public void UnionPropertiesDoesNothingWhenSourceIsNull()
        {
            var target = new TestModel() { Id = "1", Name = "Name" };

            target.Patch(null);

            target.Id.Should().Be(NanoId.Parse("1")!);
            target.Name.Should().Be("Name");
        }

        [Test]
        public void UnionPropertiesDoesNothingWhenTargetIsNull()
        {
            TestModel target = null!;
            var source = new TestModel { Id = "1", Name = "Name" };

            // ReSharper disable once ExpressionIsAlwaysNull
            target.Patch(source);
            // ReSharper disable once ExpressionIsAlwaysNull
            target.Should().BeNull();
        }

        [Test]
        public void GetListValueDoesNothingOnInvalidEnumerables()
        {
            var method =
                typeof(ModelExtensions).GetMethod("GetListValue", BindingFlags.Static | BindingFlags.NonPublic)!;
            var prop = typeof(TestGetListValueClass).GetProperty(nameof(TestGetListValueClass.Invalid))!;
            var value = method.Invoke(null, [prop, new Dictionary<string, string>(), new Dictionary<string, string>()]);
            value.Should().BeNull();
        }

        private class TestGetListValueClass : IModel
        {
            public IDictionary<string, string> Invalid { get; set; } = null!;
            public required NanoId Id { get; set; }
        }
    }
}
