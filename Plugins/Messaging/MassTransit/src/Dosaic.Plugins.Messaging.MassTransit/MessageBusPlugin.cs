using Dosaic.Hosting.Abstractions;
using Dosaic.Hosting.Abstractions.Plugins;
using Dosaic.Hosting.Abstractions.Services;
using Dosaic.Plugins.Messaging.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dosaic.Plugins.Messaging.MassTransit;

public class MessageBusPlugin(IImplementationResolver implementationResolver, MessageBusConfiguration configuration) : IPluginServiceConfiguration
{
    private record QueueMessageTypes(string Queue, Type[] MessageTypes);
    private IList<Type> GetMessageConsumers()
    {
        var messageConsumers = implementationResolver.FindAssemblies().SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsAbstract: false } && x.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageConsumer<>)));
        return messageConsumers.ToList();
    }
    private IList<QueueMessageTypes> GetQueueGroups()
    {
        var queues = (
            from messageConsumer in GetMessageConsumers()
            from @interface in messageConsumer.GetInterfaces()
                .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IMessageConsumer<>))
            select @interface.GetGenericArguments().First() into messageType
            let queueName = QueueResolver.Resolve(messageType)
            select (queueName, messageType)).ToList();
        return queues.GroupBy(x => x.queueName)
            .Select(x =>
                new QueueMessageTypes(
                    x.Key,
                    x.Select(y => y.messageType).Distinct().ToArray()))
            .ToList();
    }

    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        var consumers = GetMessageConsumers();
        foreach (var consumer in consumers)
        {
            var interfaces = consumer.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IMessageConsumer<>)).ToArray();
            foreach (var @interface in interfaces)
                serviceCollection.AddTransient(@interface, consumer);
        }
        var queueGroups = GetQueueGroups();
        var messageTypes = queueGroups.SelectMany(x => x.MessageTypes).Distinct().ToArray();
        serviceCollection.AddSingleton<IMessageValidator>(new MessageValidator(messageTypes));
        serviceCollection.AddSingleton<IMessageBus, MessageSender>();
        ConfigureMassTransit(serviceCollection, messageTypes, queueGroups);
    }

    private void ConfigureMassTransit(IServiceCollection serviceCollection, Type[] messageTypes, IList<QueueMessageTypes> queueGroups)
    {
        var consumerType = typeof(MessageConsumer<>);
        serviceCollection.AddMassTransit(opts =>
        {
            foreach (var mt in messageTypes)
                opts.AddConsumer(consumerType.MakeGenericType(mt));
            opts.UsingRabbitMq((context, config) =>
            {
                config.Host(configuration.Host, configuration.Port, configuration.VHost, h =>
                {
                    if (configuration.Username is not null && configuration.Password is not null)
                    {
                        h.Username(configuration.Username);
                        h.Password(configuration.Password);
                    }
                });
                foreach (var queueGroup in queueGroups)
                {
                    config.ReceiveEndpoint(queueGroup.Queue, configurator =>
                    {
                        foreach (var messageType in queueGroup.MessageTypes)
                            configurator.ConfigureConsumer(context, consumerType.MakeGenericType(messageType));
                    });
                }
                config.ConfigureEndpoints(context);
            });
            opts.AddHealthChecks();
            opts.ConfigureHealthCheckOptions(o =>
            {
                o.Name = "message-bus";
                o.Tags.Add(HealthCheckTag.Readiness.Value);
                o.MinimalFailureStatus = HealthStatus.Unhealthy;
            });
        });
    }
}
