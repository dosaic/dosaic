using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests;

public class QuorumQueueAttributeTests
{
    [Test]
    public void ShouldCreateWithDefaultReplicationFactor()
    {
        var attribute = new QuorumQueueAttribute();
        attribute.ReplicationFactor.Should().Be(0);
    }

    [Test]
    public void ShouldCreateWithSpecifiedReplicationFactor()
    {
        var attribute = new QuorumQueueAttribute(5);
        attribute.ReplicationFactor.Should().Be(5);
    }

    [Test]
    public void ShouldRejectNegativeReplicationFactor()
    {
        var act = () => new QuorumQueueAttribute(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ShouldAllowZeroReplicationFactor()
    {
        var attribute = new QuorumQueueAttribute(0);
        attribute.ReplicationFactor.Should().Be(0);
    }
}
