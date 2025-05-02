namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DbNanoIdPrimaryKeyAttribute(byte length, string prefix = "") : Attribute
    {
        public string Prefix { get; } = prefix;
        public byte Length { get; } = length;
        public byte LengthWithPrefix => (byte)(Length + Prefix.Length);
    }
}
