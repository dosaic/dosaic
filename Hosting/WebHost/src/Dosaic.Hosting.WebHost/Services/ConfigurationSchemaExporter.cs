using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.WebHost.Configurators;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Serilog.Events;

namespace Dosaic.Hosting.WebHost.Services
{
    /// <summary>
    ///     Describes the configuration of a service as a JSON schema, so that editors can offer completion and
    ///     validation in <c>appsettings.*</c> files. The schema is built from the
    ///     <see cref="ConfigurationAttribute" /> classes of the plugins the service was built with, plus the
    ///     sections the web host reads itself.
    ///     Keys are offered in camel case. Configuration keys are not case sensitive but a JSON schema is, so
    ///     nothing is ever declared as "not allowed" - a key in another casing is simply not completed.
    /// </summary>
    internal static class ConfigurationSchemaExporter
    {
        private const string SchemaDialect = "http://json-schema.org/draft-07/schema#";

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string ExportJson(IEnumerable<Type> types) => Export(types).ToJsonString(_jsonOptions);

        public static JsonObject Export(IEnumerable<Type> types) => Export(types
            .Where(x => x is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                        x.HasAttribute<ConfigurationAttribute>())
            .Distinct()
            .Select(x => (x.GetAttribute<ConfigurationAttribute>()!.Section, x)));

        /// <summary>Describes each type as the section it is bound to; <c>:</c> separates nested sections.</summary>
        internal static JsonObject Export(IEnumerable<(string Section, Type Type)> configurations)
        {
            var documentation = new XmlDocumentation();
            var sections = new JsonObject();
            AddHostSections(sections, documentation);
            foreach (var (section, type) in configurations
                         .OrderBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(x => x.Type.FullName, StringComparer.Ordinal))
            {
                var schema = CreateObjectSchema(type, TryCreate(type), [], documentation, false);
                AddDescription(schema, documentation.GetSummary(type));
                AddSection(sections, section, schema);
            }

            return new JsonObject
            {
                ["$schema"] = SchemaDialect,
                ["title"] = "Dosaic configuration",
                ["type"] = "object",
                ["properties"] = Sort(sections)
            };
        }

        /// <summary>The sections the web host binds itself, which no plugin configuration class describes.</summary>
        private static void AddHostSections(JsonObject sections, XmlDocumentation documentation)
        {
            AddSection(sections, "serilog", new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["minimumLevel"] = CreateSchema(typeof(LogEventLevel), LogEventLevel.Information, [], documentation,
                        false)
                }
            });
            AddSection(sections, "telemetry", CreateObjectSchema(typeof(ServiceConfigurator.OtelConfiguration),
                new ServiceConfigurator.OtelConfiguration(), [], documentation, true));
            AddSection(sections, DosaicWebHostDefaults.CorsConfigSectionName,
                CreateObjectSchema(typeof(CorsPolicy), new CorsPolicy(), [], documentation, true));
        }

        private static void AddSection(JsonObject sections, string section, JsonObject schema)
        {
            var path = section.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (path.Length == 0) return;
            var properties = sections;
            foreach (var segment in path[..^1])
            {
                var parent = FindProperty(properties, segment);
                if (parent is null)
                {
                    parent = new JsonObject { ["type"] = "object" };
                    properties[segment] = parent;
                }

                properties = GetOrAddProperties(parent);
            }

            var existing = FindProperty(properties, path[^1]);
            if (existing is null)
            {
                properties[path[^1]] = schema;
                return;
            }

            // two classes bound to the same section, or a class bound to the parent of another one
            var target = GetOrAddProperties(existing);
            foreach (var (name, value) in (schema["properties"] as JsonObject ?? []).ToList())
                if (FindProperty(target, name) is null)
                    target[name] = value?.DeepClone();
            if (existing["description"] is null && schema["description"] is { } description)
                existing["description"] = description.DeepClone();
        }

        private static JsonObject FindProperty(JsonObject properties, string name) =>
            properties.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value
                as JsonObject;

        private static JsonObject GetOrAddProperties(JsonObject schema)
        {
            if (schema["properties"] is JsonObject properties) return properties;
            properties = [];
            schema["properties"] = properties;
            return properties;
        }

        private static JsonObject Sort(JsonObject sections)
        {
            var sorted = new JsonObject();
            foreach (var (name, value) in sections.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList())
            {
                sections.Remove(name);
                sorted[name] = value;
            }

            return sorted;
        }

        private static JsonObject CreateObjectSchema(Type type, object instance, HashSet<Type> ancestors,
            XmlDocumentation documentation, bool bindsReadOnlyCollections)
        {
            var schema = new JsonObject { ["type"] = "object" };
            if (!ancestors.Add(type)) return schema;
            var properties = new JsonObject();
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                var writable = property.SetMethod?.IsPublic == true;
                if (!writable && !(bindsReadOnlyCollections && IsCollection(property.PropertyType))) continue;
                var propertySchema = CreateSchema(property.PropertyType, TryGetValue(property, instance), ancestors,
                    documentation, bindsReadOnlyCollections);
                if (propertySchema is null) continue;
                AddDescription(propertySchema,
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description ??
                    documentation.GetSummary(property));
                properties[ToCamelCase(property.Name)] = propertySchema;
            }

            ancestors.Remove(type);
            if (properties.Count > 0) schema["properties"] = properties;
            return schema;
        }

        /// <summary>Returns null for types that cannot be written in a configuration file.</summary>
        private static JsonObject CreateSchema(Type type, object defaultValue, HashSet<Type> ancestors,
            XmlDocumentation documentation, bool bindsReadOnlyCollections)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            var valueType = underlying ?? type;
            // a value object is written as the bare value it wraps
            if (GetValueObjectType(valueType) is { } wrapped)
                return CreateSchema(wrapped, null, ancestors, documentation, bindsReadOnlyCollections);
            var nullable = underlying is not null || !valueType.IsValueType;

            if (GetScalarType(valueType) is { } scalar)
            {
                var schema = new JsonObject { ["type"] = nullable ? new JsonArray(scalar.Type, "null") : scalar.Type };
                if (scalar.Format is not null) schema["format"] = scalar.Format;
                // a flags enum may be written as a combination ("A, B"), which a list of names would reject
                if (valueType.IsEnum && !valueType.IsDefined(typeof(FlagsAttribute), false))
                    schema = CreateEnumSchema(valueType, nullable);

                if (ToJsonDefault(defaultValue) is { } scalarDefault) schema["default"] = scalarDefault;
                return schema;
            }

            if (GetDictionaryValueType(valueType) is { } dictionaryValueType)
            {
                var schema = new JsonObject { ["type"] = "object" };
                var values = CreateSchema(dictionaryValueType, null, ancestors, documentation,
                    bindsReadOnlyCollections);
                if (values is not null) schema["additionalProperties"] = values;
                return schema;
            }

            if (GetElementType(valueType) is { } elementType)
            {
                var schema = new JsonObject { ["type"] = "array" };
                var items = CreateSchema(elementType, null, ancestors, documentation, bindsReadOnlyCollections);
                if (items is not null) schema["items"] = items;
                if (GetScalarType(Nullable.GetUnderlyingType(elementType) ?? elementType) is not null &&
                    defaultValue is IEnumerable defaults)
                {
                    var values = new JsonArray();
                    foreach (var value in defaults) values.Add(ToJsonDefault(value));
                    if (values.Count > 0) schema["default"] = values;
                }

                return schema;
            }

            if (!IsConfigurationObject(valueType)) return null;
            return CreateObjectSchema(valueType, defaultValue ?? TryCreate(valueType), ancestors, documentation,
                bindsReadOnlyCollections);
        }

        /// <summary>
        ///     The binder reads enum names in any casing and accepts their numeric values, while a JSON schema
        ///     <c>enum</c> is case sensitive. The exact names are listed for completion, a case-insensitive
        ///     pattern and integers keep every other spelling the binder accepts valid.
        /// </summary>
        private static JsonObject CreateEnumSchema(Type enumType, bool nullable)
        {
            var names = Enum.GetNames(enumType);
            var exact = new JsonArray();
            foreach (var name in names) exact.Add(name);
            if (nullable) exact.Add(null);
            var pattern = "^(?:" + string.Join("|", names.Select(ToCaseInsensitivePattern)) + ")$";
            return new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["enum"] = exact },
                    new JsonObject { ["type"] = "string", ["pattern"] = pattern },
                    new JsonObject { ["type"] = "integer" })
            };
        }

        /// <summary>ECMA 262 patterns have no case-insensitive flag, so every letter becomes a class.</summary>
        private static string ToCaseInsensitivePattern(string name) => string.Concat(name.Select(x =>
            char.IsLetter(x) && char.ToUpperInvariant(x) != char.ToLowerInvariant(x)
                ? $"[{char.ToUpperInvariant(x)}{char.ToLowerInvariant(x)}]"
                : Regex.Escape(x.ToString())));

        private static (string Type, string Format)? GetScalarType(Type type)
        {
            if (type.IsEnum || type == typeof(string) || type == typeof(char) || type == typeof(TimeSpan))
                return ("string", null);
            if (type == typeof(bool)) return ("boolean", null);
            if (type == typeof(Guid)) return ("string", "uuid");
            if (type == typeof(Uri)) return ("string", "uri");
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return ("string", "date-time");
            if (type == typeof(DateOnly)) return ("string", "date");
            if (type == typeof(TimeOnly)) return ("string", "time");
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
                return ("integer", null);
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return ("number", null);
            return null;
        }

        private static Type GetDictionaryValueType(Type type)
        {
            var dictionary = new[] { type }.Concat(type.GetInterfaces()).FirstOrDefault(x =>
                x.IsGenericType && (x.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                                    x.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
            // whatever the key type is, in a configuration file the keys are property names
            return dictionary?.GetGenericArguments()[1];
        }

        /// <summary>The type a Vogen value object wraps, or null when the type is not one.</summary>
        private static Type GetValueObjectType(Type type) =>
            type.GetCustomAttributes(false).Any(x => x.GetType().Name.StartsWith("ValueObjectAttribute",
                StringComparison.Ordinal))
                ? type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.PropertyType
                : null;

        private static Type GetElementType(Type type)
        {
            if (type == typeof(string)) return null;
            if (type.IsArray) return type.GetElementType();
            return new[] { type }.Concat(type.GetInterfaces())
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
        }

        private static bool IsCollection(Type type) =>
            GetDictionaryValueType(type) is not null || GetElementType(type) is not null;

        /// <summary>
        ///     Delegates, interfaces, <see cref="object" /> and framework types have no shape a configuration
        ///     file could fill in.
        /// </summary>
        private static bool IsConfigurationObject(Type type) =>
            (type.IsClass || type is { IsValueType: true, IsPrimitive: false }) &&
            !type.IsAbstract && !type.IsGenericTypeDefinition && type != typeof(object) &&
            !typeof(Delegate).IsAssignableFrom(type) && !typeof(Type).IsAssignableFrom(type) &&
            type.Namespace != "System" && type.Namespace?.StartsWith("System.", StringComparison.Ordinal) != true;

        private static JsonNode ToJsonDefault(object value) => value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            char character => JsonValue.Create(character.ToString()),
            Enum enumValue => JsonValue.Create(enumValue.ToString()),
            TimeSpan timeSpan => JsonValue.Create(timeSpan.ToString("c", CultureInfo.InvariantCulture)),
            Guid guid => JsonValue.Create(guid.ToString()),
            Uri uri => JsonValue.Create(uri.ToString()),
            decimal number => JsonValue.Create(number),
            float or double => ToJsonNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture), value),
            byte or sbyte or short or ushort or int or uint or long => JsonValue.Create(
                Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            ulong unsigned => JsonValue.Create(unsigned),
            _ => null
        };

        /// <summary>
        ///     JSON has no NaN or infinity, so such a default is left out. Within the range of decimal the value is
        ///     written as one, which keeps <c>0.1f</c> from turning into <c>0.100000001490116</c>.
        /// </summary>
        private static JsonNode ToJsonNumber(double number, object value)
        {
            if (!double.IsFinite(number)) return null;
            return Math.Abs(number) < 7.9e28
                ? JsonValue.Create(Convert.ToDecimal(value, CultureInfo.InvariantCulture))
                : JsonValue.Create(number);
        }

        private static void AddDescription(JsonObject schema, string description)
        {
            if (!string.IsNullOrWhiteSpace(description)) schema["description"] = description;
        }

        private static string ToCamelCase(string name) =>
            name.Length == 0 || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>Defaults come from a fresh instance, exactly like the binder starts from one.</summary>
        private static object TryCreate(Type type)
        {
            try
            {
                return type.GetConstructor(Type.EmptyTypes) is null && !type.IsValueType
                    ? null
                    : Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object TryGetValue(PropertyInfo property, object instance)
        {
            if (instance is null) return null;
            try
            {
                return property.GetValue(instance);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        ///     Reads <c>&lt;summary&gt;</c> texts from the XML documentation file next to an assembly. Such a
        ///     file only exists when the assembly was built with <c>GenerateDocumentationFile</c> and, for
        ///     NuGet packages, when the service copies it with <c>CopyDocumentationFilesFromPackages</c>.
        /// </summary>
        internal sealed class XmlDocumentation
        {
            private static readonly Regex _whitespace = new(@"\s+", RegexOptions.Compiled);

            private readonly Dictionary<Assembly, Dictionary<string, string>> _summaries = [];

            public string GetSummary(Type type) => Find(type.Assembly, $"T:{GetName(type)}");

            public string GetSummary(PropertyInfo property) => property.DeclaringType is null
                ? null
                : Find(property.DeclaringType.Assembly, $"P:{GetName(property.DeclaringType)}.{property.Name}");

            private static string GetName(Type type) => type.FullName?.Replace('+', '.');

            private string Find(Assembly assembly, string member)
            {
                if (!_summaries.TryGetValue(assembly, out var summaries))
                {
                    summaries = Load(GetDocumentationPath(assembly));
                    _summaries[assembly] = summaries;
                }

                return summaries.GetValueOrDefault(member);
            }

            private static string GetDocumentationPath(Assembly assembly)
            {
                try
                {
                    return string.IsNullOrEmpty(assembly.Location) ? null : Path.ChangeExtension(assembly.Location, ".xml");
                }
                catch (NotSupportedException)
                {
                    return null;
                }
            }

            internal static Dictionary<string, string> Load(string path)
            {
                var summaries = new Dictionary<string, string>(StringComparer.Ordinal);
                if (path is null || !File.Exists(path)) return summaries;
                try
                {
                    foreach (var member in XDocument.Load(path).Descendants("member"))
                    {
                        var name = member.Attribute("name")?.Value;
                        var summary = member.Element("summary");
                        if (name is null || summary is null) continue;
                        var text = ToPlainText(summary);
                        if (text.Length > 0) summaries[name] = text;
                    }
                }
                catch (Exception)
                {
                    // a broken documentation file only costs the descriptions
                }

                return summaries;
            }

            private static string ToPlainText(XElement summary)
            {
                var text = string.Concat(summary.Nodes().Select(node => node switch
                {
                    XText plain => plain.Value,
                    XElement element when element.Attribute("cref") is { } cref => cref.Value.Split('.', ':')[^1],
                    XElement element when element.Attribute("name") is { } name => name.Value,
                    XElement element => element.Value,
                    _ => ""
                }));
                return _whitespace.Replace(text, " ").Trim();
            }
        }
    }

    /// <summary>
    ///     Writes the configuration schema on request, in one of two ways:
    ///     <list type="bullet">
    ///         <item>
    ///             <c>--write-config-schema [path]</c> - the caller ends the process afterwards, instead of
    ///             starting the service.
    ///         </item>
    ///         <item>
    ///             <c>DOSAIC_WRITE_CONFIG_SCHEMA=path</c> - the process carries on, so that whatever the
    ///             arguments ask for (for example another export) runs in the same process. An argument
    ///             cannot do this: a command line parser further down would reject it as unknown.
    ///         </item>
    ///     </list>
    /// </summary>
    internal static class ConfigurationSchemaCommand
    {
        public const string Argument = "--write-config-schema";
        public const string EnvironmentVariable = "DOSAIC_WRITE_CONFIG_SCHEMA";
        public const string DefaultFileName = "appsettings.schema.json";

        /// <summary>Returns true when the arguments asked for the schema and it was written.</summary>
        public static bool TryRun(Type[] types, IEnumerable<string> args, TextWriter output)
        {
            if (!TryGetPath(args, out var path)) return false;
            Write(types, path, output);
            return true;
        }

        /// <summary>
        ///     Checks the arguments given to the host first and the arguments of the process second. Each list
        ///     is read on its own: merged, an argument at the start of one list would be taken as the path that
        ///     belongs to <see cref="Argument" /> at the end of the other.
        /// </summary>
        public static bool TryRun(Type[] types, IEnumerable<string> args, IEnumerable<string> processArgs,
            TextWriter output) =>
            TryRun(types, args, output) || TryRun(types, processArgs, output);

        /// <summary>
        ///     Returns true when the value of <see cref="EnvironmentVariable" /> asked for the schema. <c>true</c>
        ///     or <c>1</c> mean the default file name, anything else is the path. The service starts in any
        ///     case: a schema that cannot be written (read-only file system) is reported on
        ///     <paramref name="output" /> instead of thrown, and an unchanged schema is not written again, so
        ///     file watchers (<c>dotnet watch</c>) do not see a change on every start.
        /// </summary>
        public static bool TryRunFromEnvironment(Type[] types, string value, TextWriter output)
        {
            if (!TryGetPathFromEnvironment(value, out var path)) return false;
            try
            {
                Write(types, path, output);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                          or ArgumentException or System.Security.SecurityException)
            {
                output.WriteLine(
                    $"Could not write the configuration schema to {path} ({e.GetType().Name}: {e.Message})");
            }

            return true;
        }

        internal static bool TryGetPathFromEnvironment(string value, out string path)
        {
            path = value?.Trim();
            if (string.IsNullOrEmpty(path) || path is "0" ||
                path.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase))
                return false;
            if (path is "1" || path.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                path = DefaultFileName;
            return true;
        }

        private static void Write(Type[] types, string path, TextWriter output)
        {
            var fullPath = Path.GetFullPath(path);
            var content = ConfigurationSchemaExporter.ExportJson(types) + Environment.NewLine;
            if (File.Exists(fullPath) && File.ReadAllText(fullPath) == content)
            {
                output.WriteLine($"The configuration schema in {fullPath} is up to date");
                return;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, content);
            output.WriteLine($"Wrote the configuration schema to {fullPath}");
        }

        internal static bool TryGetPath(IEnumerable<string> args, out string path)
        {
            path = null;
            var arguments = (args ?? []).Where(x => x is not null).ToList();
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument.StartsWith(Argument + "=", StringComparison.OrdinalIgnoreCase))
                    path = argument[(Argument.Length + 1)..];
                else if (argument.Equals(Argument, StringComparison.OrdinalIgnoreCase))
                    path = i + 1 < arguments.Count && !arguments[i + 1].StartsWith('-') ? arguments[i + 1] : "";
                else
                    continue;
                if (string.IsNullOrWhiteSpace(path)) path = DefaultFileName;
                return true;
            }

            return false;
        }
    }
}
