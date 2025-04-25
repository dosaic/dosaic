using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    public interface ITriggerContext<T> where T : class, IModel
    {
        public ChangeSet<T> ChangeSet { get; }
        public IDb Database { get; }
    }
}
