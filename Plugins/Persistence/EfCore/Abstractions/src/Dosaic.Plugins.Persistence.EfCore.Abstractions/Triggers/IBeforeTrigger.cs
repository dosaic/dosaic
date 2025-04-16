using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    public interface IBeforeTrigger<T> where T : class, IModel
    {
        public Task HandleBeforeAsync(ITriggerContext<T> context, CancellationToken cancellationToken);
    }
}