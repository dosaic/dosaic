using FluentAssertions;
using NUnit.Framework;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;

namespace Dosaic.Hosting.Abstractions.Tests.Attributes
{
    public class MiddlewareAttributeTests
    {
        [Test]
        public void CanSpecifyMiddlewareOrder()
        {
            var attr = new MiddlewareAttribute(101);
            attr.Order.Should().Be(101);
            attr.Should().BeAssignableTo<Attribute>();
            var attrUsage = attr.GetType().GetAttribute<AttributeUsageAttribute>();
            attrUsage.Should().NotBeNull();
            attrUsage.ValidOn.Should().Be(AttributeTargets.Class);
        }
    }
}
