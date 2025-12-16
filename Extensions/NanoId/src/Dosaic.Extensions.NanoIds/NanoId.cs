using System.Reflection;
using System.Text.Json.Serialization;
using Dosaic.Hosting.Abstractions.Attributes;
using NanoidDotNet;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Dosaic.Extensions.NanoIds
{
    public class NanoIdJsonConverter : JsonConverter<NanoId>
    {
        public override NanoId Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert,
            System.Text.Json.JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return new NanoId(value!);
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, NanoId value,
            System.Text.Json.JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    public class NanoIdYamlConverter : IYamlConverter
    {
        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var scalar = parser.Consume<Scalar>();
            return new NanoId(scalar.Value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            if (value is NanoId nanoId)
            {
                emitter.Emit(new Scalar(nanoId.Value));
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(NanoIdJsonConverter))]
    [YamlTypeConverter(typeof(NanoIdYamlConverter))]
    public readonly struct NanoId(string value) :
        IComparable,
        IComparable<NanoId>,
        IEquatable<NanoId>
    {
        public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is not NanoId id ? throw new ArgumentException("Object is not a NanoId") : CompareTo(id);
        }

        public int CompareTo(NanoId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(NanoId other)
        {
            return Value == other.Value;
        }

        public static NanoId NewId<T>() where T : INanoId
        {
            return NewId(typeof(T));
        }

        public static NanoId NewId(Type type)
        {
            var nanoIdAttribute = type.GetCustomAttribute<NanoIdAttribute>();
            if (nanoIdAttribute == null)
                throw new ArgumentException($"Type {type.Name} does not have a NanoIdAttribute.");
            return new NanoId(
                $"{nanoIdAttribute.Prefix}{Nanoid.Generate(NanoIdConfig.Alphabet, nanoIdAttribute.Length)}");
        }

        public override bool Equals(object obj)
        {
            return obj is NanoId id && Equals(id);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(NanoId left, NanoId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NanoId left, NanoId right)
        {
            return !Equals(left, right);
        }

        public string ToString(IFormatProvider formatProvider)
        {
            FormattableString formatedString = $"{nameof(Value)}: {Value}";
            return formatedString.ToString(formatProvider);
        }

        public override string ToString()
        {
            return Value;
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
            IFormatProvider provider)
        {
            return destination.TryWrite(provider, $"{nameof(Value)}: {Value}",
                out charsWritten);
        }

        public static implicit operator NanoId(string value) => new(value);
        // public static implicit operator NanoId?(string value) => Parse(value);

        public static implicit operator string(NanoId value)
        {
            return value.Value;
        }

        // public static implicit operator string(NanoId? value)
        // {
        //     return value?.Value;
        // }

        public static NanoId? Parse(string value)
        {
            return value is null ? (NanoId?)null : new NanoId(value);
        }
    }

    public interface INanoId
    {
        NanoId Id { get; set; }
    }
}
