namespace Dosaic.Plugins.Messaging.MassTransit;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsumerTimeoutAttribute : Attribute
{
    public ConsumerTimeoutAttribute(int timeoutSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutSeconds);
        TimeoutSeconds = timeoutSeconds;
    }

    public int TimeoutSeconds { get; }
}
