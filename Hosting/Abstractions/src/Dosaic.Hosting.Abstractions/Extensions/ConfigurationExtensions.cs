using Microsoft.Extensions.Configuration;

namespace Dosaic.Hosting.Abstractions.Extensions
{
    public static class ConfigurationExtensions
    {
        public static T BindToSection<T>(this IConfiguration configuration, string sectionKey) where T : new()
        {
            var config = new T();
            configuration.Bind(sectionKey, config);
            return config;
        }
    }
}
