using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    public record TriggerContext<T>(ChangeSet<T> ChangeSet, IDb Database, IServiceProvider ServiceProvider)
        : ITriggerContext<T> where T : class, IModel;
}