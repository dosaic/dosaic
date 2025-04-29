using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    [TriggerOrder(Order = int.MaxValue)]
    public class HistoryTrigger<T>(IUserIdProvider userIdProvider, IDateTimeProvider dateTimeProvider) : IAfterTrigger<T>
        where T : class, IModel, IHistory
    {
        private readonly string[] _excludedProperties = typeof(T).GetProperties()
            .Where(x => x.GetCustomAttribute<ExcludeFromHistoryAttribute>() != null).Select(x => x.Name.ToLowerInvariant()).ToArray();
        private History<T> GetHistoryEntry(ModelChange<T> context)
        {
            var changes = context.GetChanges().FilterKeys(x => !_excludedProperties.Contains(x.ToLowerInvariant()));
            if (changes.Count == 0) return null;
            return new History<T>
            {
                Id = NanoId.NewId<History<T>>(),
                ForeignId = (context.Entity?.Id ?? context.PreviousEntity?.Id)!,
                ChangeSet = changes.ToJson(),
                ModifiedBy = userIdProvider.IsUserInteraction ? userIdProvider.UserId : userIdProvider.FallbackUserId,
                ModifiedUtc = dateTimeProvider.UtcNow,
                State = context.State
            };
        }

        public async Task HandleAfterAsync(ITriggerContext<T> context, CancellationToken cancellationToken)
        {
            var histories = context.ChangeSet.Select(x => GetHistoryEntry(x)!).Where(x => x != null).ToArray();
            if (histories.Length == 0) return;
            await context.Database.Get<History<T>>().AddRangeAsync(histories, cancellationToken);
        }
    }
}
