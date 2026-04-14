using Dosaic.Hosting.Abstractions.Plugins;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ
{
    public class TickerQMessageSchedulerPlugin : IPluginServiceConfiguration, IMessageBusConfigurator
    {
        private readonly TickerQMessageSchedulerConfiguration _configuration;

        public TickerQMessageSchedulerPlugin(TickerQMessageSchedulerConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IMessageScheduler, TickerQMessageScheduler>();
        }

        public void ConfigureMassTransit(IBusRegistrationConfigurator opts)
        {
            // TickerQ handles scheduling externally; no MassTransit scheduler configuration needed.
        }
    }
}
