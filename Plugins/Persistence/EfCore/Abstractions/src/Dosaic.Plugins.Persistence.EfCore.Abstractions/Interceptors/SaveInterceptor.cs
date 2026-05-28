using System.Diagnostics;
using System.Reflection;
using Dosaic.Hosting.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors
{
    public class SaveInterceptor(IServiceProvider serviceProvider, IDb db)
    {
        private List<TTrigger> GetTriggers<TTrigger>()
        {
            return serviceProvider.GetService<IEnumerable<TTrigger>>()?
                       .Where(x => x != null)
                       .OrderBy(x => x!.GetType().GetCustomAttribute<TriggerOrderAttribute>()?.Order ?? 0)
                       .ToList()
                   ?? [];
        }

        private static Activity StartTriggerActivity(object trigger, Type modelType, string phase, int changeSetCount)
        {
            var triggerType = trigger.GetType();
            var activity = Tracing.Source.StartActivity($"EfCore.Trigger.{phase}.{triggerType.Name}", ActivityKind.Internal);
            activity?.SetTag("trigger.type", triggerType.FullName);
            activity?.SetTag("trigger.model", modelType.FullName);
            activity?.SetTag("trigger.phase", phase);
            activity?.SetTag("trigger.changeset.count", changeSetCount);
            return activity;
        }

        private async Task HandleBeforeAsync<T>(ChangeSet<T> changeSet, CancellationToken cancellationToken)
            where T : class, IModel
        {
            var triggers = GetTriggers<IBeforeTrigger<T>>();
            if (triggers.Count == 0) return;
            var context = new TriggerContext<T>(changeSet, db);
            foreach (var trigger in triggers)
            {
                using var activity = StartTriggerActivity(trigger, typeof(T), "before", changeSet.Count);
                try
                {
                    await trigger.HandleBeforeAsync(context, cancellationToken);
                    activity?.SetOkStatus();
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
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
                using var activity = StartTriggerActivity(trigger, typeof(T), "after", changeSet.Count);
                try
                {
                    await trigger.HandleAfterAsync(context, cancellationToken);
                    activity?.SetOkStatus();
                }
                catch (Exception ex)
                {
                    activity?.SetErrorStatus(ex);
                    throw;
                }
            }
        }

        private static readonly MethodInfo _handleAfterAsyncMethod =
            typeof(SaveInterceptor).GetMethod(nameof(HandleAfterAsync),
                BindingFlags.NonPublic | BindingFlags.Instance)!;

        private async Task DispatchTriggersAsync(MethodInfo methodInfo, ChangeSet changeSet, CancellationToken cancellationToken)
        {
            foreach (var (modelType, cs) in changeSet.GetTypedChangeSets())
            {
                await (methodInfo.MakeGenericMethod(modelType).Invoke(this, [cs, cancellationToken]) as Task)!;
            }
        }

        public Task BeforeSaveAsync(ChangeSet changeSet, CancellationToken cancellationToken = default)
        {
            return DispatchTriggersAsync(_handleBeforeAsyncMethod, changeSet, cancellationToken);
        }

        public async Task AfterSaveAsync(ChangeSet changeSet, CancellationToken cancellationToken = default)
        {
            await DispatchTriggersAsync(_handleAfterAsyncMethod, changeSet, cancellationToken);
            var historyWriter = serviceProvider.GetService<IHistoryWriter>();
            if (historyWriter is null) return;
            using var activity = Tracing.Source.StartActivity("EfCore.History.Write", ActivityKind.Internal);
            activity?.SetTag("history.writer.type", historyWriter.GetType().FullName);
            activity?.SetTag("history.changeset.count", changeSet.Count);
            try
            {
                await historyWriter.WriteAsync(changeSet, db, cancellationToken);
                activity?.SetOkStatus();
            }
            catch (Exception ex)
            {
                activity?.SetErrorStatus(ex);
                throw;
            }
        }
    }
}
