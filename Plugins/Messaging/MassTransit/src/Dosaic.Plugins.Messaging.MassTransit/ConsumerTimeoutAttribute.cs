namespace Dosaic.Plugins.Messaging.MassTransit;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsumerTimeoutAttribute(int timeoutSeconds) : Attribute
{
    public int TimeoutSeconds { get; } = timeoutSeconds;
}
