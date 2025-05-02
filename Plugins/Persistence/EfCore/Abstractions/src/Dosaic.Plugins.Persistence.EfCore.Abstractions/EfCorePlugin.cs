using System.Diagnostics;
using System.Reflection;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Monitoring;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public class EfCorePlugin(
        IImplementationResolver implementationResolver,
        IEfCoreConfigurator[] configurators,
        ILogger<EfCorePlugin> logger) : IPluginServiceConfiguration,
        IPluginApplicationConfiguration, IPluginHealthChecksConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddOpenTelemetry().WithTracing(builder =>
            {
                var enrichEfCoreWithActivity = EnrichEfCoreWithActivity;
                configurators.ForEach(x => x.ConfigureEntityFrameworkCoreInstrumentation(enrichEfCoreWithActivity));
                builder
                    .AddEntityFrameworkCoreInstrumentation(enrichEfCoreWithActivity);
                configurators.ForEach(x => x.ConfigureOtelWithTracing(builder));
            });

            RegisterBusinessLogicInterceptors(serviceCollection);
            serviceCollection.AddSingleton<IBusinessLogicInterceptor, BusinessLogicInterceptor>();

            serviceCollection.RegisterEventProcessors(implementationResolver, logger);
            serviceCollection.RegisterTriggers(implementationResolver, typeof(IBeforeTrigger<>), logger);
            serviceCollection.RegisterTriggers(implementationResolver, typeof(IAfterTrigger<>), logger);
        }

        private void RegisterBusinessLogicInterceptors(IServiceCollection serviceCollection)
        {
            var interceptors = GetBusinessLogicInterceptors();
            foreach (var interceptor in interceptors)
            {
                foreach (var serviceType in interceptor.GetInterfaces().Where(x =>
                             x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IBusinessLogic<>)))
                {
                    serviceCollection.AddTransient(serviceType, interceptor);
                }
            }
        }

        private List<Type> GetBusinessLogicInterceptors()
        {
            var interceptors = implementationResolver.FindTypes(type =>
                type is { IsClass: true, IsAbstract: false } && type.Implements(typeof(IBusinessLogic<>)));
            return interceptors;
        }

        internal static void EnrichEfCoreWithActivity(EntityFrameworkInstrumentationOptions options)
        {
            options.EnrichWithIDbCommand = (activity, command) =>
            {
                var stateDisplayName = $"{command.CommandType} main";
                activity.DisplayName = stateDisplayName;
                activity.SetTag("db.name", stateDisplayName);
            };
        }

        public void ConfigureApplication(IApplicationBuilder applicationBuilder)
        {
            DiagnosticListener.AllListeners.Subscribe(new DiagnosticObserver());
        }

        public void ConfigureHealthChecks(IHealthChecksBuilder healthChecksBuilder)
        {
            var dbContexts = implementationResolver.FindAssemblies().SelectMany(x => x.GetTypes())
                .Where(x => x.IsClass && !x.IsAbstract && x.IsAssignableTo(typeof(DbContext)))
                .ToList();
            var registerHealthCheck = typeof(ServiceCollectionExtensions)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(x =>
                    x.GetParameters().Length == 1 &&
                    x.GetParameters()[0].ParameterType == typeof(IHealthChecksBuilder));

            foreach (var dbContext in dbContexts)
                registerHealthCheck.MakeGenericMethod(dbContext).Invoke(null, [healthChecksBuilder]);
        }
    }

    public interface IEfCoreConfigurator : IPluginConfigurator
    {
        void ConfigureEntityFrameworkCoreInstrumentation(Action<EntityFrameworkInstrumentationOptions> options);
        void ConfigureOtelWithTracing(TracerProviderBuilder options);
    }
}
