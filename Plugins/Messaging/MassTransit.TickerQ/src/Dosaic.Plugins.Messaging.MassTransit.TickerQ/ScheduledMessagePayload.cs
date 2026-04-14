using System.Text.Json;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ
{
    public enum ScheduledMessageMode
    {
        Send,
        Publish
    }

    public class ScheduledMessagePayload
    {
        public string MessageTypeAssemblyQualifiedName { get; set; }
        public string MessageJson { get; set; }
        public string DestinationAddress { get; set; }
        public ScheduledMessageMode Mode { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public static ScheduledMessagePayload Create(Type messageType, object message,
            ScheduledMessageMode mode, Uri destination = null,
            IDictionary<string, string> headers = null)
        {
            return new ScheduledMessagePayload
            {
                MessageTypeAssemblyQualifiedName = messageType.AssemblyQualifiedName,
                MessageJson = JsonSerializer.Serialize(message, messageType),
                DestinationAddress = destination?.ToString(),
                Mode = mode,
                Headers = headers != null
                    ? new Dictionary<string, string>(headers)
                    : new Dictionary<string, string>()
            };
        }

        public (Type MessageType, object Message) Deserialize()
        {
            var messageType = Type.GetType(MessageTypeAssemblyQualifiedName);
            if (messageType == null)
                throw new InvalidOperationException(
                    $"Cannot resolve message type: {MessageTypeAssemblyQualifiedName}");
            var message = JsonSerializer.Deserialize(MessageJson, messageType);
            return (messageType, message);
        }
    }
}
