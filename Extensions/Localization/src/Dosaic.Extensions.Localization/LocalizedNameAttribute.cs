
namespace Dosaic.Extensions.Localization
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LocalizedNameAttribute : Attribute
    {
        public string En { get; set; }
        public string De { get; set; }

        public LocalizedNameAttribute(string en = "", string de = "")
        {
            En = en;
            De = de;
        }
    }
}
