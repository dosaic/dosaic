namespace Dosaic.Hosting.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationAttribute : Attribute
    {
        private readonly string[] _sections;

        public ConfigurationAttribute(string section)
            : this(section.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {

        }
        public ConfigurationAttribute(params string[] sections)
        {
            _sections = sections;
        }

        public string Section => string.Join(":", _sections);

        /// <summary>
        ///     Keys below <see cref="Section" /> the type cannot work without. They are named in the error a host
        ///     reports when the whole section is missing. Empty (default) names none.
        /// </summary>
        public string[] RequiredKeys { get; set; } = [];
    }
}
