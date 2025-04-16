using System.Diagnostics;
using System.Reflection;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public class EfCorePlugin(IImplementationResolver implementationResolver) : IPluginServiceConfiguration,
        IPluginApplicationConfiguration, IPluginHealthChecksConfiguration
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            RegisterBusinessLogicInterceptors(serviceCollection);
            serviceCollection.AddSingleton<IBusinessLogicInterceptor, BusinessLogicInterceptor>();

            serviceCollection.AddOpenTelemetry().WithTracing(builder => builder
                .AddEntityFrameworkCoreInstrumentation(EnrichEfCoreWithActivity));
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
            var registerHc = typeof(ServiceCollectionExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(x =>
                    x.GetParameters().Length == 1 &&
                    x.GetParameters()[0].ParameterType == typeof(IHealthChecksBuilder));

            foreach (var dbContext in dbContexts)
                registerHc.MakeGenericMethod(dbContext).Invoke(null, [healthChecksBuilder]);
        }
    }
}
