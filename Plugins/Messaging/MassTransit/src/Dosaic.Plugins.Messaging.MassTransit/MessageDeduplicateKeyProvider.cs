using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dosaic.Plugins.Messaging.MassTransit
{
    public sealed class MessageDeduplicateKeyProvider(MessageBusConfiguration configuration)
        : IMessageDeduplicateKeyProvider
    {
        private readonly ConcurrentDictionary<Type, Delegate> _registry = new();

        public void Register<T>(Func<T, string> keyFactory)
            => _registry[typeof(T)] = keyFactory;

        public string TryGetKey(object message)
        {
            if (!configuration.Deduplication) return null;
            if (message is null) return null;
            var type = message.GetType();

            if (_registry.TryGetValue(type, out var del))
            {
                var key = (string)del.DynamicInvoke(message)!;
                return string.IsNullOrWhiteSpace(key) ? null : key;
            }

            var hash = GetHash(message);
            return $"{type.FullName}:{hash}";
        }

        private static string GetHash(object instance)
        {
            var json = JsonSerializer.Serialize(instance);
            var inputBytes = Encoding.UTF8.GetBytes(json);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
