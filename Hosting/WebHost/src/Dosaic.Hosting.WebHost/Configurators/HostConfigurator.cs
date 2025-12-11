using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.WebHost.Extensions;
using Dosaic.Hosting.WebHost.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dosaic.Hosting.WebHost.Configurators
{
    internal class HostConfigurator(WebApplicationBuilder builder, ILogger logger)
    {
        public const string HostAdditionalconfigpathsEnvVarName = "HOST:ADDITIONALCONFIGPATHS";

        public void Configure()
        {
            ConfigureLogging();
            ConfigureWebHost();
        }

        internal static void ConfigureAppConfiguration(ConfigurationManager configurationManager, string[] args)
        {
#pragma warning disable ASP0013

            configurationManager.Sources.Clear();
            var additionalAppsettingFolders = ResolveAdditionalConfigPaths(configurationManager, args);
            configurationManager.AddCommandLine(args);

            var appsettingFiles = FindAllRootAppsettingFiles();

            foreach (var path in additionalAppsettingFolders)
            {
                appsettingFiles.AddRange(FindAppSettingFilesRecursive(path, "json", "yaml", "yml"));
            }

            AddConfigurationFiles(configurationManager, appsettingFiles);

            configurationManager.AddEnvVariables();
#pragma warning restore ASP0013
        }

        private static List<string> FindAllRootAppsettingFiles()
        {
            return FindAppSettingFiles("json", "yaml", "yml").ToList();
        }

        private static IEnumerable<string> ResolveAdditionalConfigPaths(ConfigurationManager configurationManager,
            string[] args)
        {
            configurationManager.Sources.Clear();
            configurationManager.AddCommandLine(args);

            var appsettingFiles = FindAllRootAppsettingFiles();
            AddConfigurationFiles(configurationManager, appsettingFiles);

            configurationManager.AddEnvVariables();
            var resolveAdditionalConfigPaths = GetAdditionalConfigPaths(configurationManager);
            configurationManager.Sources.Clear();
            return resolveAdditionalConfigPaths;
        }

        private static void AddConfigurationFiles(ConfigurationManager configurationManager, List<string> allSettings)
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

        private static IEnumerable<string> GetAdditionalConfigPaths(ConfigurationManager configurationManager)
        {
            var paths = new List<string>();

            var envVar = configurationManager.GetValue<string>(HostAdditionalconfigpathsEnvVarName);
            if (!string.IsNullOrWhiteSpace(envVar))
            {
                paths.AddRange(envVar.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()));
            }

            return paths.Distinct();
        }

        private static IEnumerable<string> FindAppSettingFiles(params string[] extensions)
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory)
                .Select(x => x.Split(Path.DirectorySeparatorChar).Last())
                .Where(x => !string.IsNullOrEmpty(x) && x.StartsWith("appsettings.", StringComparison.InvariantCulture))
                .Where(x => extensions.Any(e => x.EndsWith(e, StringComparison.InvariantCultureIgnoreCase)))
                .OrderBy(x => x.Split('.').Length);

            return files;
        }

        private static IEnumerable<string> FindAppSettingFilesRecursive(string directory, params string[] extensions)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            var files = Directory.EnumerateFiles(directory, "appsettings.*", SearchOption.AllDirectories)
                .Where(x => extensions.Any(e => x.EndsWith($".{e}", StringComparison.InvariantCultureIgnoreCase)));

            return files;
        }

        private void ConfigureLogging()
        {
            builder.Logging.ClearProviders();
            builder.Host.UseStructuredLogging();
        }

        private void ConfigureWebHost()
        {
            var config = builder.Configuration;
            var urls = config.GetValue<string>("host:urls")
                       ?? config.GetValue<string>("aspnetcore:urls")
                       ?? "http://+:8080";
            var maxRequestSize = config.GetValue<long?>("host:maxRequestSize") ?? 8388608; // 8 MB default
            logger.LogInformation("Hosting urls {HostingUrls}", urls.Replace(",", " , "));
            builder.WebHost.UseKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = maxRequestSize;
                options.AddServerHeader = false;
            }).UseUrls(urls.Split(','));
        }
    }
}
