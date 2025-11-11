using System.Globalization;
using AwesomeAssertions;
using Dosaic.Plugins.Messaging.Abstractions;
using NUnit.Framework;

namespace Dosaic.Plugins.Messaging.MassTransit.Tests
{
    public class MessageDeduplicateKeyProviderTests
    {
        private static MessageBusConfiguration _configuration;
        private static TestMessage _testMessage;

        [SetUp]
        public void Setup()
        {
            _configuration = new MessageBusConfiguration() { Host = "localhost", Deduplication = true };
            _testMessage = new TestMessage(123);
        }

        [Test]
        public void KeyProviderReturnsNullForEmptyMessage()
        {
            var provider = new MessageDeduplicateKeyProvider(_configuration);
            var result = provider.TryGetKey(null);
            result.Should().BeNull();
        }

        [Test]
        public void KeyProviderReturnsNullForDisabeldDeduplication()
        {
            _configuration.Deduplication = false;
            var provider = new MessageDeduplicateKeyProvider(_configuration);
            var result = provider.TryGetKey(_testMessage);
            result.Should().BeNull();
        }

        [Test]
        public void KeyProviderReturnsHashKey()
        {
            var provider = new MessageDeduplicateKeyProvider(_configuration);
            var result = provider.TryGetKey(_testMessage);
            result.Should().NotBeNull();
            result.Should().Contain(_testMessage.GetType().FullName);
            result.Should().Contain("evto/uxs1JI4GlFx06z/HsE5zzK6enBcT2qRi0dZF+o=");

        }

        [Test]
        public void KeyProviderReturnsConfiguredKey()
        {
            var provider = new MessageDeduplicateKeyProvider(_configuration);
            provider.Register<TestMessage>(x => $"{typeof(TestMessage).BaseType}:{x.Id}");
            var result = provider.TryGetKey(_testMessage);
            result.Should().NotBeNull();
            result.Should().Contain(_testMessage.GetType().BaseType!.ToString());
            result.Should().Contain(_testMessage.Id.ToString(CultureInfo.InvariantCulture));
        }

        private record TestMessage(int Id) : IMessage;

    }
}
