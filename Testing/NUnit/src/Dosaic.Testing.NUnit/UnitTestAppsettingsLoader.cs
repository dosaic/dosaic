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
            var appsettingFiles = FindAllRootAppsettingFiles();
            AddConfigurationFiles(config, appsettingFiles);
            config.AddEnvironmentVariables();
            if (additionalFiles != null)
            {
                foreach (var file in additionalFiles)
                {
                    config.AddFile(file, optional: true);
                }
            }
            return config.Build();
        }

        public static T LoadSection<T>(IConfiguration config = null, string[] additionalFiles = null)
        {
            config ??= LoadLocalAppsettings(additionalFiles);
            var sectionName = typeof(T).GetAttribute<ConfigurationAttribute>().Section;
            var section = config.GetSection(sectionName);
            var result = section.Get<T>() ?? throw new InvalidOperationException($"Section '{sectionName}' not found in configuration.");
            return result;

        }

        private static void AddFile(this ConfigurationBuilder configurationManager, string file, bool optional = true, bool reloadOnChange = true)
        {
            if (file.EndsWith(".json"))
                configurationManager.AddJsonFile(file, optional, reloadOnChange);
            else if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                configurationManager.AddYamlFile(file, optional, reloadOnChange);
        }

        private static List<string> FindAllRootAppsettingFiles()
        {
            return FindAppSettingFiles("json", "yaml", "yml").ToList();
        }

        private static void AddConfigurationFiles(ConfigurationBuilder configurationManager, List<string> allSettings)
        {
            var orderedSettings = allSettings
                .Distinct()
                .OrderBy(x => Path.GetFileName(x).Split('.').Length)
                .ToList();

            foreach (var file in orderedSettings.Where(x => !IsSecretsFile(x)))
                configurationManager.AddFile(file);

            foreach (var file in orderedSettings.Where(IsSecretsFile))
                configurationManager.AddFile(file);
        }

        private static bool IsSecretsFile(string filename) => filename.EndsWith(".secrets.yaml") ||
                                                              filename.EndsWith(".secrets.yml") ||
                                                              filename.EndsWith(".secrets.json");

        private static IEnumerable<string> FindAppSettingFiles(params string[] extensions)
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory)
                .Select(x => x.Split(Path.DirectorySeparatorChar).Last())
                .Where(x => !string.IsNullOrEmpty(x) && x.StartsWith("appsettings.", StringComparison.InvariantCulture))
                .Where(x => extensions.Any(e => x.EndsWith(e, StringComparison.InvariantCultureIgnoreCase)))
                .OrderBy(x => x.Split('.').Length);

            return files;
        }
    }
}
