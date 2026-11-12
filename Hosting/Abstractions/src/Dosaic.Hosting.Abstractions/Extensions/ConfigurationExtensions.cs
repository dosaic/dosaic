using System.Globalization;
using System.Reflection;
using Dosaic.Hosting.Abstractions.Attributes;
using Microsoft.Extensions.Configuration;

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

        /// <exception cref="InvalidOperationException">
        ///     The section does not exist and <paramref name="type" /> is no collection. The message names the
        ///     section and the <see cref="ConfigurationAttribute.RequiredKeys" /> of the type.
        /// </exception>
        public static object GetSection(this IConfiguration configuration, string sectionKey, Type type)
        {
            var configurationSection = configuration.GetSection(sectionKey);
            if (!configurationSection.Exists() && GetDictionaryValueType(type) is null && GetElementType(type) is null)
                throw new InvalidOperationException(MissingSectionMessage(sectionKey, type));
            try
            {
                return ConfigurationSectionToObject(configurationSection, type).Serialize(SerializationMethod.Yaml)
                    .Deserialize(type, SerializationMethod.Yaml);
            }
            catch (Exception e)
            {
                throw new AggregateException($"Failed to bind configuration section '{sectionKey}' to type '{type.FullName}'.", e);
            }
        }

        private static string MissingSectionMessage(string sectionKey, Type type)
        {
            var requiredKeys = type.GetCustomAttribute<ConfigurationAttribute>()?.RequiredKeys ?? [];
            var message = $"Configuration section '{sectionKey}' is missing. {type.Name} is bound from it.";
            if (requiredKeys.Length == 0) return message;
            var keys = requiredKeys.Select(key => $"{sectionKey}:{key}").ToList();
            var environment = keys.Select(key => key.Replace(':', '_').ToUpperInvariant());
            return $"{message} Set at least {string.Join(", ", keys)} (environment: {string.Join(", ", environment)}).";
        }

        /// <param name="section">The section to convert.</param>
        /// <param name="type">
        ///     The type the section is bound to, or null when it is not known (polymorphic or untyped values).
        ///     It is only consulted for dictionaries, everything else is derived from the keys.
        /// </param>
        private static object ConfigurationSectionToObject(IConfigurationSection section, Type type)
        {
            var children = section.GetChildren().ToList();
            if (GetDictionaryValueType(type) is { } valueType)
                return DictionaryToObject(children, valueType);
            if (children.All(child => int.TryParse(child.Key, out _)))
            {
                var elementType = GetElementType(type);
                if (children.All(child => !child.GetChildren().Any()))
                {
                    return children.Select(child => ParseValue(child.Value)).ToList();
                }

                return children.OrderBy(child => int.Parse(child.Key, CultureInfo.InvariantCulture))
                    .Select(child => ConfigurationSectionToObject(child, elementType))
                    .ToList();
            }

            var result = new Dictionary<string, object>();
            foreach (var child in children)
            {
                result[child.Key] = child.GetChildren().Any()
                    ? ConfigurationSectionToObject(child, GetPropertyType(type, child.Key))
                    : ParseValue(child.Value);
            }

            return result;
        }

        /// <summary>
        ///     The keys of a dictionary are names, so they stay a map even when every key is a number. A nested
        ///     section below a dictionary of plain values can only come from a key that contained the
        ///     separator: environment variables turn every <c>_</c> into <c>:</c>, so <c>QUEUES_MY_QUEUE=3</c>
        ///     arrives as <c>queues:my:queue</c>. Such a key is joined back with <c>_</c>.
        /// </summary>
        private static Dictionary<string, object> DictionaryToObject(List<IConfigurationSection> children,
            Type valueType)
        {
            var result = new Dictionary<string, object>();
            var plainValues = IsPlainValue(valueType);
            foreach (var child in children)
            {
                if (!child.GetChildren().Any())
                    result[child.Key] = ParseValue(child.Value);
                else if (!plainValues)
                    result[child.Key] = ConfigurationSectionToObject(child, valueType);
                else
                    foreach (var (key, value) in Flatten(child, child.Key))
                        result[key] = value;
            }

            return result;
        }

        private static IEnumerable<(string Key, object Value)> Flatten(IConfigurationSection section, string key)
        {
            foreach (var child in section.GetChildren())
            {
                var childKey = key + "_" + child.Key;
                if (child.GetChildren().Any())
                    foreach (var entry in Flatten(child, childKey))
                        yield return entry;
                else
                    yield return (childKey, ParseValue(child.Value));
            }

            if (section.Value is not null) yield return (key, ParseValue(section.Value));
        }

        private static bool IsPlainValue(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                   type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) || type == typeof(Uri);
        }

        private static Type GetDictionaryValueType(Type type)
        {
            if (type is null) return null;
            var dictionary = new[] { type }.Concat(type.GetInterfaces()).FirstOrDefault(x =>
                x.IsGenericType && (x.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                                    x.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
            return dictionary?.GetGenericArguments()[1];
        }

        private static Type GetElementType(Type type)
        {
            if (type is null || type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();
            return new[] { type }.Concat(type.GetInterfaces())
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
        }

        private static Type GetPropertyType(Type type, string key)
        {
            if (type is null || GetElementType(type) is not null) return null;
            // GetProperty with IgnoreCase throws when two properties differ in casing only
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(x => string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase))?.PropertyType;
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
