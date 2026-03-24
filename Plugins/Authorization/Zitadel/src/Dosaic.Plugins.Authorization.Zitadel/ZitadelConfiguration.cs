using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Authorization.Zitadel
{
    [Configuration("Zitadel")]
    public class ZitadelConfiguration
    {
        public required string ProjectId { get; set; }
        public required string OrganizationId { get; set; }
        public required string Host { get; set; }
        public bool UseHttps { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
        public bool ValidateEndpoints { get; set; } = true;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationInMinutes { get; set; } = 1;
        public string CacheKeyPrefix { get; set; } = "ZITADEL_";
        public required string JwtProfile { get; set; }
        public string ServiceAccount { get; set; }
    }
}
