using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;
using NUnit.Framework;

namespace Dosaic.Hosting.Abstractions.Tests.Attributes
{
    public class MiddlewareAttributeTests
    {
        [Test]
        public void CanSpecifyMiddlewareOrder()
        {
            var attr = new MiddlewareAttribute(101);
            attr.Order.Should().Be(101);
            attr.Mode.Should().Be(MiddlewareMode.BeforePlugins);
            attr.Should().BeAssignableTo<Attribute>();
            var attrUsage = attr.GetType().GetAttribute<AttributeUsageAttribute>();
            attrUsage.Should().NotBeNull();
            attrUsage.ValidOn.Should().Be(AttributeTargets.Class);
        }

        [Test]
        public void CanSpecifyMiddlewareMode()
        {
            var attr = new MiddlewareAttribute(5, MiddlewareMode.AfterPlugins);
            attr.Order.Should().Be(5);
            attr.Mode.Should().Be(MiddlewareMode.AfterPlugins);
        }

        [Test]
        public void DefaultsToMaxOrderAndBeforePlugins()
        {
            var attr = new MiddlewareAttribute();
            attr.Order.Should().Be(int.MaxValue);
            attr.Mode.Should().Be(MiddlewareMode.BeforePlugins);
        }

        [Test]
        public void CanSpecifyNoRegistrationMode()
        {
            var attr = new MiddlewareAttribute(1, MiddlewareMode.NoRegistration);
            attr.Mode.Should().Be(MiddlewareMode.NoRegistration);
        }
    }
}
