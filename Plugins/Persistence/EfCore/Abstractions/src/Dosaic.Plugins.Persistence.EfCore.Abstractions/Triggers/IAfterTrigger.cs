using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    public interface IAfterTrigger<T> where T : class, IModel
    {
        public Task HandleAfterAsync(ITriggerContext<T> context, CancellationToken cancellationToken);
    }
}