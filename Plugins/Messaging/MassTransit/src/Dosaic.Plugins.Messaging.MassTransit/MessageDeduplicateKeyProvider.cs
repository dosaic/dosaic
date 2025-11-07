using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dosaic.Plugins.Messaging.MassTransit
{
    public sealed class MessageDeduplicateKeyProvider(MessageBusConfiguration configuration)
        : IMessageDeduplicateKeyProvider
    {
        private readonly ConcurrentDictionary<Type, Delegate> _registry = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false
        };

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


            // var json = JsonSerializer.Serialize(message, JsonOpts);
            // using var sha = SHA256.Create();
            // var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
            var hash = GetSH1Hash(message);
            return $"{type.FullName}:{hash}";
        }

        // extension method candidate
        private string GetSH1Hash(object instance)
        {
            var serializer = new DataContractSerializer(instance.GetType());
            using var memoryStream = new MemoryStream();
            serializer.WriteObject(memoryStream, instance);

            return BitConverter.ToString(SHA1.HashData(memoryStream)).Replace("-", "");
        }
    }
}
