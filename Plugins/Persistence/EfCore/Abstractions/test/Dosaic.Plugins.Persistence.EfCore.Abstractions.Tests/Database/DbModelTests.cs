using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    public class DbModelTests
    {
        [Test]
        public void GetPropertiesReturnsAllReadableWritableProperties()
        {
            var props = DbModel.GetProperties(typeof(TestModel));

            props.Should().NotBeNull();
            props.Should().Contain(p => p.Name == "Name");
            props.Should().Contain(p => p.Name == "Id");
            props.Should().Contain(p => p.Name == "OwnedModel");
            props.Should().Contain(p => p.Name == "_PropertyWithUnderscore");
            props.Should().Contain(p => p.Name == "PropertyName");
            props.Should().Contain(p => p.Name == "EnumProperty");
            props.Should().Contain(p => p.Name == "NullableEnumProperty");
        }

        [Test]
        public void GetPropertiesGenericReturnsAllReadableWritableProperties()
        {
            var props = DbModel.GetProperties<TestModel>();

            props.Should().NotBeNull();
            props.Should().Contain(p => p.Name == "Name");
            props.Should().Contain(p => p.Name == "Id");
        }

        [Test]
        public void GetModelByNameReturnsCorrectType()
        {
            var model = DbModel.GetModelByName(typeof(TestModel), "TestModel");

            model.Should().Be(typeof(TestModel));
        }

        [Test]
        public void GetModelByNameIsCaseInsensitive()
        {
            var model = DbModel.GetModelByName(typeof(TestModel), "testmodel");

            model.Should().Be(typeof(TestModel));
        }

        [Test]
        public void GetModelByNameThrowsWhenModelNotFound()
        {
            var act = () => DbModel.GetModelByName(typeof(TestModel), "NonExistentModel");

            act.Should().Throw<KeyNotFoundException>();
        }

        [Test]
        public void GetModelPropertiesReturnsAllModelsInAssembly()
        {
            var props = DbModel.GetProperties(typeof(TestModel));
            var propsAudit = DbModel.GetProperties(typeof(TestAuditModel));
            var propsSub = DbModel.GetProperties(typeof(SubTestModel));

            props.Should().NotBeNull();
            propsAudit.Should().NotBeNull();
            propsSub.Should().NotBeNull();
        }

    }
}
