using Dosaic.Hosting.Abstractions.Attributes;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.Extensions.Configuration;

namespace Dosaic.Testing.NUnit
{
    public static class UnitTestAppsettingsLoader
    {
        public static IConfiguration LoadLocalAppsettings(string[] additionalFiles = null)
        {
            var config = new ConfigurationBuilder();
            config.AddFile("appsettings.yaml", optional: true);
            config.AddFile("appsettings.secrets.yaml", optional: true);
            if (additionalFiles != null)
            {
                foreach (var file in additionalFiles)
                {
                    config.AddFile(file, optional: true);
                }
            }
            return config.Build();
        }

        public static void AddFile(this ConfigurationBuilder configurationManager, string file, bool optional = true, bool reloadOnChange = true)
        {
            if (file.EndsWith(".json"))
                configurationManager.AddJsonFile(file, optional, reloadOnChange);
            else if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                configurationManager.AddYamlFile(file, optional, reloadOnChange);
        }

        public static T LoadSection<T>(string[] additionalFiles = null)
        {
            var config = LoadLocalAppsettings(additionalFiles);
            var sectionName = typeof(T).GetAttribute<ConfigurationAttribute>().Section;
            var section = config.GetSection(sectionName);
            var result = section.Get<T>();
            if (result == null)
                throw new InvalidOperationException($"Section '{sectionName}' not found in configuration.");
            return result;
        }
    }
}
