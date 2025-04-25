using FluentAssertions;
using NUnit.Framework;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Triggers
{
    [TestFixture]
    public class TriggerOrderAttributeTests
    {
        [Test]
        public void OrderPropertyIsSetThroughConstructor()
        {
            var attribute = new TriggerOrderAttribute { Order = 42 };

            attribute.Order.Should().Be(42);
        }

        [Test]
        public void OrderPropertyDefaultsToZero()
        {
            var attribute = new TriggerOrderAttribute();

            attribute.Order.Should().Be(0);
        }

        [Test]
        public void SetOrderValueToMinimum()
        {
            var attribute = new TriggerOrderAttribute { Order = int.MinValue };

            attribute.Order.Should().Be(int.MinValue);
        }

        [Test]
        public void SetOrderValueToMaximum()
        {
            var attribute = new TriggerOrderAttribute { Order = int.MaxValue };

            attribute.Order.Should().Be(int.MaxValue);
        }

        [Test]
        public void AttributeAllowsOnlyClassTargets()
        {
            var attributes = typeof(TriggerOrderAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false);
            attributes.Should().HaveCount(1);

            var usage = (AttributeUsageAttribute)attributes[0];
            usage.ValidOn.Should().Be(AttributeTargets.Class);
            usage.Inherited.Should().BeFalse();
        }
    }
}
