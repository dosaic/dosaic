namespace Dosaic.Plugins.Messaging.MassTransit;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class QuorumQueueAttribute : Attribute
{
    public QuorumQueueAttribute(int replicationFactor = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(replicationFactor);
        ReplicationFactor = replicationFactor;
    }

    public int ReplicationFactor { get; }
}
