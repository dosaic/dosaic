using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Management.Unleash
{
    [Configuration("unleash")]
    public class UnleashConfiguration
    {
        public string AppName { get; set; }
        public string ApiUri { get; set; }
        public string ApiToken { get; set; }
        public string InstanceTag { get; set; }
    }
}
