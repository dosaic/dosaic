namespace Dosaic.Extensions.NanoIds
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NanoIdAttribute(byte length, string prefix = "") : Attribute
    {
        public string Prefix { get; } = prefix;
        public byte Length { get; } = length;
        public byte LengthWithPrefix => (byte)(Length + Prefix.Length);
    }
}
