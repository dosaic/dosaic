using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers
{
    [TriggerOrder(Order = int.MinValue)]
    internal class BusinessLogicTrigger<T>(IBusinessLogicInterceptor businessLogicInterceptor) : IBeforeTrigger<T>, IAfterTrigger<T>
        where T : class, IModel
    {
        public async Task HandleBeforeAsync(ITriggerContext<T> context, CancellationToken cancellationToken)
        {
            foreach (var entry in context.ChangeSet)
                await businessLogicInterceptor.InterceptBeforeAsync(entry.Entity!, entry.State, cancellationToken);
        }

        public async Task HandleAfterAsync(ITriggerContext<T> context, CancellationToken cancellationToken)
        {
            foreach (var entry in context.ChangeSet)
                await businessLogicInterceptor.InterceptAfterAsync(entry.Entity!, entry.State, cancellationToken);
        }
    }
}
