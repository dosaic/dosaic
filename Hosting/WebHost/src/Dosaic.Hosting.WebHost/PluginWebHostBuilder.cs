using Dosaic.Hosting.Abstractions.Configuration;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Hosting.WebHost.Configurators;
using Dosaic.Hosting.WebHost.Extensions;
using Dosaic.Hosting.WebHost.Logging;
using Dosaic.Hosting.WebHost.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Dosaic.Hosting.WebHost
{
    public class PluginWebHostBuilder
    {
        private readonly WebApplicationBuilder _webApplicationBuilder;
        private readonly IImplementationResolver _implementationResolver;
        private readonly ILogger<PluginWebHostBuilder> _logger;

        private readonly Type[] _resolvedTypes;
        private readonly HostConfiguration _hostConfiguration;

        // ReSharper disable once MemberCanBePrivate.Global
        public PluginWebHostBuilder(Type[] types, params string[] args)
        {
            // has to happen before anything is configured: writing the schema must not need a database, and the
            // process ends here because whatever runs the host afterwards would not know this argument.
            // The process arguments are checked as well, so it also works when args are not passed along.
            if (ConfigurationSchemaCommand.TryRun(types, [.. args ?? [], .. Environment.GetCommandLineArgs().Skip(1)],
                    Console.Out))
                Environment.Exit(0);
            SystemExtensions.SetDefaultCultureInfo();
            _webApplicationBuilder = WebApplication.CreateBuilder(args);
            HostConfigurator.ConfigureAppConfiguration(_webApplicationBuilder.Configuration, args);
            StructuredLoggingExtensions.CreateBootstrapLogger(
                _webApplicationBuilder.Configuration.GetValue<LogEventLevel?>("serilog:minimumLevel")
                ?? LogEventLevel.Information);
            _logger = GetLoggerFactory().CreateLogger<PluginWebHostBuilder>();
            _resolvedTypes = types;
            _hostConfiguration = HostConfiguration.Load(_webApplicationBuilder.Configuration);
            _implementationResolver = GetImplementationResolver(_webApplicationBuilder);
        }

        public static PluginWebHostBuilder Create(Type[] types, params string[] args) => new(types, args);

        /// <summary>
        ///     Writes a JSON schema of the configuration of the given plugin types to <paramref name="path" />,
        ///     for completion and validation of <c>appsettings.*</c> files in editors. The same happens when
        ///     the service is started with <c>--write-config-schema [path]</c>.
        /// </summary>
        public static void WriteConfigurationSchema(Type[] types, string path) =>
            ConfigurationSchemaCommand.TryRun(types, [ConfigurationSchemaCommand.Argument, path], TextWriter.Null);

        public static void RunDefault(Type[] types, params string[] args)
        {
            Create(types, args).Build().Run();
        }

        public IHost Build()
        {
            var webApplicationConfigurator = new HostConfigurator(_webApplicationBuilder, _logger, _hostConfiguration);
            webApplicationConfigurator.Configure();
            LogPlugins();

            var serviceConfigurator = new ServiceConfigurator(_logger, _webApplicationBuilder.Configuration,
                _webApplicationBuilder.Services, _implementationResolver, _hostConfiguration);
            serviceConfigurator.Configure();
            var host = _webApplicationBuilder.Build();
            var appConfigurator = new AppConfigurator(_logger, host, _implementationResolver);
            appConfigurator.Configure();
            _implementationResolver.ClearInstances();
            return host;
        }

        private static ILoggerFactory GetLoggerFactory() => new SerilogLoggerFactory(Log.Logger);

        private IImplementationResolver GetImplementationResolver(WebApplicationBuilder webApplicationBuilder)
        {
            var loggerFactory = GetLoggerFactory();
            var defaultArgs = new Dictionary<Type, object>
            {
                { typeof(IHostEnvironment), webApplicationBuilder.Environment },
                { typeof(ILogger), new Func<Type, object>(t => loggerFactory.CreateLogger(t)) },
                {
                    typeof(ILogger<>), new Func<Type, object>(t => typeof(LoggerFactoryExtensions).GetMethods()
                        .First(x => x.Name == nameof(LoggerFactoryExtensions.CreateLogger) &&
                                    x.IsGenericMethodDefinition)
                        .MakeGenericMethod(t)
                        .Invoke(null, [loggerFactory])!)
                },
                { typeof(ILoggerFactory), loggerFactory },
                { typeof(HostConfiguration), _hostConfiguration }
            };

            // use type resolving from source generator so we can use native AOT compilation
            return new TypeImplementationResolver(webApplicationBuilder.Configuration, _resolvedTypes, defaultArgs);
        }

        private void LogPlugins()
        {
            var pluginActivateables = _implementationResolver.FindTypes(x => x.Implements<IPluginActivateable>());
            _logger.LogInformation("Found {PluginCount} plugins", pluginActivateables.Count);
            pluginActivateables.ForEach(activateable =>
            {
                _logger.LogInformation("Loaded plugin {ActivateablesText}", activateable.GetPluginName());
            });
        }
    }
}
