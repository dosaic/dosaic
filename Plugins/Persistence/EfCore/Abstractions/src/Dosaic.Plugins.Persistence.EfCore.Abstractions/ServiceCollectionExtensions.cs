using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public static class ServiceCollectionExtensions
    {
        public static IHealthChecksBuilder AddEfCoreContext<TContext>(this IHealthChecksBuilder healthChecksBuilder)
            where TContext : DbContext
        {
            healthChecksBuilder.AddDbContextCheck<TContext>(typeof(TContext).Name,
                tags: [HealthCheckTag.Readiness.Value]);
            return healthChecksBuilder;
        }

        public static void AddDbMigratorService<TDbContext>(this IServiceCollection serviceCollection)
            where TDbContext : DbContext
        {
            serviceCollection.AddHostedService<DbMigratorService<TDbContext>>();
        }

        public static void MigrateEfContexts<TDbContext>(this IApplicationBuilder applicationBuilder)
            where TDbContext : DbContext
        {
            var logger = applicationBuilder.ApplicationServices.GetRequiredService<ILogger<EfCorePlugin>>();
            applicationBuilder.ApplicationServices.GetServices<TDbContext>().ToList()
                .ForEach(dbContext =>
                {
                    var dbContextName = dbContext.GetType().Name;
                    logger.LogDebug("Migrating '{DbContextName}'", dbContextName);
                    dbContext.Database.Migrate();
                    logger.LogDebug("Migrated '{DbContextName}'", dbContextName);
                });
        }

        public static void RegisterTriggers(this IServiceCollection serviceCollection,
            IImplementationResolver implementationResolver, Type triggerType, ILogger logger)
        {
            var triggers = implementationResolver.FindAssemblies().SelectMany(x => x.GetTypes()).Where(x =>
                x is { IsAbstract: false, IsClass: true } && x.Implements(triggerType));
            foreach (var trigger in triggers)
            {
                if (trigger.IsGenericType)
                {
                    serviceCollection.AddTransient(triggerType, trigger);
                    logger.LogDebug(
                        $"Registering trigger {trigger.Name}");
                }
                else
                {
                    var modelType = trigger.GetInterfaces()
                        .Single(x => x.IsGenericType && x.GetGenericTypeDefinition() == triggerType)
                        .GenericTypeArguments[0];
                    var implementedTriggerType = triggerType.MakeGenericType(modelType);
                    serviceCollection.AddTransient(implementedTriggerType, trigger);
                    logger.LogDebug(
                        $"Registering trigger {implementedTriggerType.Name} {modelType.Name}");
                }
            }
        }

        public static void RegisterEventProcessors(this IServiceCollection serviceCollection,
            IImplementationResolver implementationResolver, ILogger logger)
        {
            var eventProcessors = implementationResolver.FindTypes(type =>
                type is { IsClass: true, IsAbstract: false } && type.Implements(typeof(IEventProcessor<>)));

            foreach (var processor in eventProcessors)
            {
                foreach (var serviceType in processor.GetInterfaces().Where(x =>
                             x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEventProcessor<>)))
                {
                    logger.LogDebug(
                        $"Registering event processor {serviceType.Name} {serviceType.GetGenericTypeDefinition()}");
                    serviceCollection.AddTransient(serviceType, processor);
                }
            }
        }
    }
}
