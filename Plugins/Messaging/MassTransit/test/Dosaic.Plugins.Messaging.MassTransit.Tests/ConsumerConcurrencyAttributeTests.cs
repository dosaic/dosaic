using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class ConsumerConcurrencyAttributeTests
{
    [Test]
    public void ShouldCreateWithSpecifiedConcurrencyLimit()
    {
        var attribute = new ConsumerConcurrencyAttribute(10);
        attribute.ConcurrencyLimit.Should().Be(10);
    }

    [Test]
    public void ShouldRejectZeroConcurrencyLimit()
    {
        var act = () => new ConsumerConcurrencyAttribute(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ShouldRejectNegativeConcurrencyLimit()
    {
        var act = () => new ConsumerConcurrencyAttribute(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
