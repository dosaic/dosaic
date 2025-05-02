using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    public class DbEnumAttributeTests
    {
        [Test]
        public void ConstructorSetsProperly()
        {
            var attr = new DbEnumAttribute("EnumName", "dbo");

            attr.Name.Should().Be("EnumName");
            attr.Schema.Should().Be("dbo");
        }

        [Test]
        public void DbNameCombinesSchemaAndName()
        {
            var attr = new DbEnumAttribute("EnumName", "dbo");

            attr.DbName.Should().Be("dbo.EnumName");
        }

        [Test]
        public void DbNameWorksWithCustomSchema()
        {
            var attr = new DbEnumAttribute("CustomEnum", "custom");

            attr.DbName.Should().Be("custom.CustomEnum");
        }

        [Test]
        public void AttributeTargetIsEnum()
        {
            var attrType = typeof(DbEnumAttribute);
            var usageAttr =
                (AttributeUsageAttribute)Attribute.GetCustomAttribute(attrType, typeof(AttributeUsageAttribute));

            usageAttr.Should().NotBeNull();
            usageAttr!.ValidOn.Should().Be(AttributeTargets.Enum);
        }
    }

}
