using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Caching.Redis
{
    [Configuration("redisCache")]
    public class RedisCacheConfiguration
    {
        public string ConnectionString { get; set; } = null!;
    }
}
