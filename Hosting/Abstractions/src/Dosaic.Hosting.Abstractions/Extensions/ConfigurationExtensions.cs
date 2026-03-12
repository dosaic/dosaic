using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class ConfigurationExtensions
    {
        public static T BindToSection<T>(this IConfiguration configuration, string sectionKey) where T : new()
        {
            var config = new T();
            configuration.Bind(sectionKey, config);
            return config;
        }

        public static object GetSection(this IConfiguration configuration, string sectionKey, Type type)
        {
            var section = configuration.GetSection(sectionKey);

            var serializationMethod = ResolveSerializationMethod(section);
            var obj = ConfigurationSectionToObject(section);

            return serializationMethod != null
                ? obj.Serialize(serializationMethod.Value)
                    .Deserialize(type, serializationMethod.Value)
                : obj.Serialize().Deserialize(type);
        }

        internal static SerializationMethod? ResolveSerializationMethod(IConfigurationSection section)
        {
            var rootField = typeof(ConfigurationSection)
                .GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = rootField?.GetValue(section) as IConfigurationRoot;

            IConfigurationProvider configurationProvider = null;
            if (root != null)
            {
                var firstChild = section.GetChildren().FirstOrDefault();
                if (firstChild != null)
                {
                    configurationProvider = root.Providers
                        .Reverse()
                        .FirstOrDefault(p => p.TryGet(firstChild.Path, out _));
                }
            }

            if (configurationProvider == null)
            {
                return null;
            }

            return configurationProvider switch
            {
                _ when configurationProvider.GetType().FullName ==
                       "NetEscapades.Configuration.Yaml.YamlConfigurationProvider" =>
                    SerializationMethod.Yaml,
                JsonConfigurationProvider => SerializationMethod.Json,
                EnvironmentVariablesConfigurationProvider => SerializationMethod.Json,
                CommandLineConfigurationProvider => SerializationMethod.Json,
                MemoryConfigurationProvider => SerializationMethod.Json,
                _ => throw new NotSupportedException(
                    $"Unsupported configuration provider: {configurationProvider.GetType().FullName}")
            };
        }

        private static object ConfigurationSectionToObject(IConfigurationSection section)
        {
            var children = section.GetChildren().ToList();
            if (children.All(child => int.TryParse(child.Key, out _)))
            {
                if (children.All(child => !child.GetChildren().Any()))
                {
                    return children.Select(child => ParseValue(child.Value)).ToList();
                }

                return children.OrderBy(child => int.Parse(child.Key, CultureInfo.InvariantCulture))
                    .Select(ConfigurationSectionToObject)
                    .ToList();
            }

            var result = new Dictionary<string, object>();
            foreach (var child in children)
            {
                result[child.Key] = child.GetChildren().Any()
                    ? ConfigurationSectionToObject(child)
                    : ParseValue(child.Value);
            }

            return result;
        }

        private static object ParseValue(string value)
        {
            if (value is null) return null;

            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }

            if (decimal.TryParse(value, out var decimalValue))
            {
                return decimalValue;
            }

            return value;
        }
    }
}
