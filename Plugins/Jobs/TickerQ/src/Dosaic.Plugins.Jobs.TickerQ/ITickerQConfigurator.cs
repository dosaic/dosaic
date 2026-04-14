using Dosaic.Hosting.Abstractions.Plugins;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Dosaic.Plugins.Jobs.TickerQ
{
    public interface ITickerQConfigurator : IPluginConfigurator
    {
        bool IncludesPersistence { get; }
        void Configure(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options);
    }
}
