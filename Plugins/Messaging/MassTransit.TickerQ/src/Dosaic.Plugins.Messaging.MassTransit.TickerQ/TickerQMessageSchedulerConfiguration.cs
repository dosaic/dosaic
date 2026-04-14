using Dosaic.Hosting.Abstractions.Attributes;

namespace Dosaic.Plugins.Messaging.MassTransit.TickerQ
{
    [Configuration("tickerqScheduler")]
    public class TickerQMessageSchedulerConfiguration
    {
        public string FunctionName { get; set; } = "masstransit-scheduled-send";
    }
}
