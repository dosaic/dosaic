namespace Dosaic.Plugins.Messaging.MassTransit;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsumerConcurrencyAttribute(int concurrencyLimit) : Attribute
{
    public int ConcurrencyLimit { get; } = concurrencyLimit;
}
