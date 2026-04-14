using AwesomeAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ.Tests
{
    public class ScheduledMessagePayloadTests
    {
        [Test]
        public void CreateSerializesMessageCorrectly()
        {
            var message = new TestMessage { Name = "test", Value = 42 };
            var destination = new Uri("rabbitmq://localhost/test-queue");

            var payload = ScheduledMessagePayload.Create(
                typeof(TestMessage), message, ScheduledMessageMode.Send, destination);

            payload.MessageTypeAssemblyQualifiedName.Should().Contain("TestMessage");
            payload.DestinationAddress.Should().Be(destination.ToString());
            payload.Mode.Should().Be(ScheduledMessageMode.Send);
            payload.MessageJson.Should().Contain("test");
            payload.Headers.Should().NotBeNull();
        }

        [Test]
        public void CreateForPublishHasNoDestination()
        {
            var message = new TestMessage { Name = "pub", Value = 1 };

            var payload = ScheduledMessagePayload.Create(
                typeof(TestMessage), message, ScheduledMessageMode.Publish);

            payload.DestinationAddress.Should().BeNull();
            payload.Mode.Should().Be(ScheduledMessageMode.Publish);
        }

        [Test]
        public void DeserializeReturnsCorrectTypeAndMessage()
        {
            var original = new TestMessage { Name = "roundtrip", Value = 99 };
            var payload = ScheduledMessagePayload.Create(
                typeof(TestMessage), original, ScheduledMessageMode.Send,
                new Uri("rabbitmq://localhost/q"));

            var (messageType, message) = payload.Deserialize();

            messageType.Should().Be(typeof(TestMessage));
            var deserialized = message as TestMessage;
            deserialized.Should().NotBeNull();
            deserialized.Name.Should().Be("roundtrip");
            deserialized.Value.Should().Be(99);
        }

        [Test]
        public void DeserializeThrowsForUnknownType()
        {
            var payload = new ScheduledMessagePayload
            {
                MessageTypeAssemblyQualifiedName = "NonExistent.Type, NonExistent.Assembly",
                MessageJson = "{}",
                DestinationAddress = "rabbitmq://localhost/q"
            };

            var action = () => payload.Deserialize();
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*Cannot resolve message type*");
        }

        [Test]
        public void CreateWithHeadersPreservesHeaders()
        {
            var headers = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            var payload = ScheduledMessagePayload.Create(
                typeof(TestMessage), new TestMessage(), ScheduledMessageMode.Send,
                new Uri("rabbitmq://localhost/q"), headers);

            payload.Headers.Should().HaveCount(2);
            payload.Headers["key1"].Should().Be("value1");
            payload.Headers["key2"].Should().Be("value2");
        }

        public class TestMessage
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }
    }
}
