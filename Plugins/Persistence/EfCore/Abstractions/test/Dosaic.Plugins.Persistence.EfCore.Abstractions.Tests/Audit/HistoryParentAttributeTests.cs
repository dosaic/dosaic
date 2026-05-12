using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryParentAttributeTests
    {
        [Test]
        public void StoresRequiredFields()
        {
            var attr = new HistoryParentAttribute(typeof(TestHistoryModel), nameof(TestHistoryChildModel.ParentId));
            attr.ParentType.Should().Be(typeof(TestHistoryModel));
            attr.ParentIdProperty.Should().Be(nameof(TestHistoryChildModel.ParentId));
            attr.Collection.Should().BeNull();
        }

        [Test]
        public void SupportsOptionalCollectionOverride()
        {
            var attr = new HistoryParentAttribute(typeof(TestHistoryModel), nameof(TestHistoryChildModel.ParentId))
            {
                Collection = nameof(TestHistoryModel.Children)
            };
            attr.Collection.Should().Be(nameof(TestHistoryModel.Children));
        }

        [Test]
        public void ThrowsWhenParentTypeNull()
        {
            var act = () => new HistoryParentAttribute(null, "ParentId");
            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ThrowsWhenParentIdPropertyNull()
        {
            var act = () => new HistoryParentAttribute(typeof(TestHistoryModel), null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
