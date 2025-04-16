using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using NanoidDotNet;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers
{
    [Serializable]
    public class NanoId :
        IComparable,
        IComparable<NanoId>,
        IEquatable<NanoId>
    {
        public NanoId(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Value { get; }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;
            if (obj is not NanoId id)
                throw new ArgumentException("Object is not a NanoId");
            return CompareTo(id);
        }

        public int CompareTo(NanoId other)
        {
            return other == null
                ? 1
                : string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(NanoId other)
        {
            if (other == null)
                return false;
            return Value == other.Value;
        }

        public static NanoId NewId<T>() where T : IModel
        {
            return NewId(typeof(T));
        }

        public static NanoId NewId(Type type)
        {
            var nanoIdAttribute = type.GetAttribute<DbNanoIdPrimaryKeyAttribute>();
            return new NanoId($"{nanoIdAttribute.Prefix}{Nanoid.Generate(NanoIds.Alphabet, nanoIdAttribute.Length)}");
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            return Equals((NanoId)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(NanoId left, NanoId right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null))
                return false;
            return !ReferenceEquals(right, null) && left.Equals(right);
        }

        public static bool operator !=(NanoId left, NanoId right)
        {
            return !(left == right);
        }

        public string ToString(string format, IFormatProvider formatProvider)
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

        public static implicit operator NanoId(string value)
        {
            return new NanoId(value);
        }

        public static implicit operator string(NanoId value)
        {
            return value.Value;
        }

        public static NanoId Parse(string value)
        {
            return value == null ? null : new NanoId(value);
        }
    }
}
