using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.WebHost.Extensions;
using Dosaic.Hosting.WebHost.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dosaic.Hosting.WebHost.Configurators
{
    internal class HostConfigurator
    {
        private readonly WebApplicationBuilder _builder;
        private readonly ILogger _logger;

        public HostConfigurator(WebApplicationBuilder builder, ILogger logger)
        {
            _builder = builder;
            _logger = logger;
        }

        public void Configure()
        {
            ConfigureLogging();
            ConfigureWebHost();
        }

        internal static void ConfigureAppConfiguration(ConfigurationManager configurationManager)
        {
#pragma warning disable ASP0013

            configurationManager.Sources.Clear();

            var additionalPaths = GetAdditionalConfigPaths();
            var allSettings = new List<string>();

            allSettings.AddRange(FindAppSettingFiles("json", "yaml", "yml"));

            foreach (var path in additionalPaths)
            {
                allSettings.AddRange(FindAppSettingFilesRecursive(path, "json", "yaml", "yml"));
            }

            var orderedSettings = allSettings
                .Distinct()
                .OrderBy(x => Path.GetFileName(x).Split('.').Length)
                .ToList();

            foreach (var file in orderedSettings.Where(x => !IsSecretsFile(x)))
                configurationManager.AddFile(file);

            foreach (var file in orderedSettings.Where(IsSecretsFile))
                configurationManager.AddFile(file);

            configurationManager.AddEnvVariables();
#pragma warning restore ASP0013
        }

        private static bool IsSecretsFile(string filename) => filename.EndsWith(".secrets.yaml") ||
                                                             filename.EndsWith(".secrets.yml") ||
                                                             filename.EndsWith(".secrets.json");

        private static IEnumerable<string> GetAdditionalConfigPaths()
        {
            var paths = new List<string>();

            var envVar = Environment.GetEnvironmentVariable("DOSAIC_HOST_ADDITIONALCONFIGPATHS");
            if (!string.IsNullOrWhiteSpace(envVar))
            {
                paths.AddRange(envVar.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()));
            }

            var argsVar = Environment.GetCommandLineArgs()
                .SkipWhile(x => !x.Equals("--additional-config-paths", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(argsVar))
            {
                paths.AddRange(argsVar.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()));
            }

            var resolvedPaths = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (Directory.Exists(fullPath))
                    {
                        resolvedPaths.Add(fullPath);
                    }
                }
                catch (Exception)
                {
                    // Skip invalid paths
                }
            }

            return resolvedPaths.Distinct().ToList();
        }

        private static IEnumerable<string> FindAppSettingFiles(params string[] extensions)
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory)
                .Select(x => x.Split(Path.DirectorySeparatorChar).Last())
                .Where(x => !string.IsNullOrEmpty(x) && x.StartsWith("appsettings.", StringComparison.InvariantCulture))
                .Where(x => extensions.Any(e => x.EndsWith(e, StringComparison.InvariantCultureIgnoreCase)))
                .OrderBy(x => x.Split('.').Length)
                .ToList();

            return files;
        }

        private static IEnumerable<string> FindAppSettingFilesRecursive(string directory, params string[] extensions)
        {
            if (!Directory.Exists(directory))
                return Enumerable.Empty<string>();

            try
            {
                var files = Directory.EnumerateFiles(directory, "appsettings.*", SearchOption.AllDirectories)
                    .Where(x => extensions.Any(e => x.EndsWith($".{e}", StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();

                return files;
            }
            catch (UnauthorizedAccessException)
            {
                return Enumerable.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Enumerable.Empty<string>();
            }
        }

        private void ConfigureLogging()
        {
            _builder.Logging.ClearProviders();
            _builder.Host.UseStructuredLogging();
        }

        private void ConfigureWebHost()
        {
            var config = _builder.Configuration;
            var urls = config.GetValue<string>("host:urls")
                       ?? config.GetValue<string>("aspnetcore:urls")
                       ?? "http://+:8080";
            var maxRequestSize = config.GetValue<long?>("host:maxRequestSize") ?? 8388608; // 8 MB default
            _logger.LogInformation("Hosting urls {HostingUrls}", urls.Replace(",", " , "));
            _builder.WebHost.UseKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = maxRequestSize;
                options.AddServerHeader = false;
            }).UseUrls(urls.Split(','));
        }
    }
}
