namespace Dosaic.Plugins.Messaging.MassTransit;

/// <summary>
///     Opts a message type in or out of trace-linking, overriding
///     <see cref="MessageBusConfiguration.UseTraceLinks" />. Apply to the message class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TraceLinkAttribute(bool enabled = true) : Attribute
{
    public bool Enabled { get; } = enabled;
}
