using Dosaic.Hosting.Abstractions.Plugins;
using MassTransit;

namespace Dosaic.Plugins.Messaging.MassTransit
{
    public interface IMessageBusConfigurator : IPluginConfigurator
    {
        void ConfigureMassTransit(IBusRegistrationConfigurator opts);
        void ConfigureRabbitMq(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator config);

        void ConfigureReceiveEndpoint(IBusRegistrationContext context, Uri queue,
            IRabbitMqReceiveEndpointConfigurator configurator);
    }
}
