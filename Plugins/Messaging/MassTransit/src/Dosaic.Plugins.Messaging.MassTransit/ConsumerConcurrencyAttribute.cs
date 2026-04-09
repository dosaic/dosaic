namespace Dosaic.Plugins.Messaging.MassTransit;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsumerConcurrencyAttribute : Attribute
{
    public ConsumerConcurrencyAttribute(int concurrencyLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrencyLimit);
        ConcurrencyLimit = concurrencyLimit;
    }

    public int ConcurrencyLimit { get; }
}
