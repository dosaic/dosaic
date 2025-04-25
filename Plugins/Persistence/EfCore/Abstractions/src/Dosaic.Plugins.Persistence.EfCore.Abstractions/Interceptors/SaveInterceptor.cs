using System.Reflection;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors
{
    internal class SaveInterceptor(IServiceProvider serviceProvider, IDb db)
    {
        private List<TTrigger> GetTriggers<TTrigger>()
        {
            return serviceProvider.GetService<IEnumerable<TTrigger>>()?
                       .Where(x => x != null)
                       .OrderBy(x => x!.GetType().GetCustomAttribute<TriggerOrderAttribute>()?.Order ?? 0)
                       .ToList()
                   ?? [];
        }

        private async Task HandleBeforeAsync<T>(ChangeSet<T> changeSet, CancellationToken cancellationToken)
            where T : class, IModel
        {
            var triggers = GetTriggers<IBeforeTrigger<T>>();
            if (triggers.Count == 0) return;
            var context = new TriggerContext<T>(changeSet, db);
            foreach (var trigger in triggers)
            {
                await trigger.HandleBeforeAsync(context, cancellationToken);
            }
        }

        private static readonly MethodInfo _handleBeforeAsyncMethod =
            typeof(SaveInterceptor).GetMethod(nameof(HandleBeforeAsync),
                BindingFlags.NonPublic | BindingFlags.Instance)!;

        private async Task HandleAfterAsync<T>(ChangeSet<T> changeSet, CancellationToken cancellationToken)
            where T : class, IModel
        {
            var triggers = GetTriggers<IAfterTrigger<T>>();
            if (triggers.Count == 0) return;
            var context = new TriggerContext<T>(changeSet, db);
            foreach (var trigger in triggers)
            {
                await trigger.HandleAfterAsync(context, cancellationToken);
            }
        }

        private static readonly MethodInfo _handleAfterAsyncMethod =
            typeof(SaveInterceptor).GetMethod(nameof(HandleAfterAsync),
                BindingFlags.NonPublic | BindingFlags.Instance)!;

        private async Task HandleAsync(MethodInfo methodInfo, ChangeSet changeSet, CancellationToken cancellationToken)
        {
            foreach (var (modelType, cs) in changeSet.GetTypedChangeSets())
            {
                await (methodInfo.MakeGenericMethod(modelType).Invoke(this, [cs, cancellationToken]) as Task)!;
            }
        }

        public Task BeforeSaveAsync(ChangeSet changeSet, CancellationToken cancellationToken = default)
        {
            return HandleAsync(_handleBeforeAsyncMethod, changeSet, cancellationToken);
        }

        public Task AfterSaveAsync(ChangeSet changeSet, CancellationToken cancellationToken = default)
        {
            return HandleAsync(_handleAfterAsyncMethod, changeSet, cancellationToken);
        }
    }
}
