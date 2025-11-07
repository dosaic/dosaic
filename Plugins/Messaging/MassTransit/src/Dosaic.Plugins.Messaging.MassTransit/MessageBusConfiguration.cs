using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Messaging.MassTransit;

[Configuration("messageBus")]
public class MessageBusConfiguration
{
    public required string Host { get; set; }
    public string VHost { get; set; } = "/";
    public ushort Port { get; set; } = 5672;
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseRetry { get; set; }
    public int MaxRetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public int MaxRedeliveryCount { get; set; } = 3;
    public int RedeliveryDelaySeconds { get; set; } = 30;
    public bool Deduplication { get; set; }
}
