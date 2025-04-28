using System.Diagnostics.CodeAnalysis;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    internal static class ChangeTrackerExtensions
    {
        private static IEnumerable<EntityEntry> GetEntities(this ChangeTracker changeTracker) =>
            changeTracker.Entries()
                .Where(x => x.Entity is IModel);

        [ExcludeFromCodeCoverage(Justification = "This is a simple mapping function. Hard to fake wrong enum values")]
        private static ChangeState? ToChangeState(this EntityState state) => state switch
        {
            EntityState.Added => ChangeState.Added,
            EntityState.Modified => ChangeState.Modified,
            EntityState.Deleted => ChangeState.Deleted,
            _ => null
        };

        public static ChangeSet GetChangeSet(this ChangeTracker changeTracker)
        {
            var changeSet = new ChangeSet();
            foreach (var entity in changeTracker.GetEntities())
            {
                var state = entity.State.ToChangeState();
                if (!state.HasValue) continue;
                var old = entity.OriginalValues.ToObject();
                var model = entity.Entity;
                changeSet.Add(ModelChange.Create(state.Value, model, old));
            }
            return changeSet;
        }

        public static ChangeSet UpdateChangeSet(this ChangeTracker changeTracker, ChangeSet changeSet)
        {
            foreach (var entry in changeTracker.GetEntities())
            {
                var model = entry.Entity as IModel;
                var existing = changeSet.SingleOrDefault(x => x.Entity?.Id == model!.Id);
                if (existing is null)
                {
                    continue;
                }
                var change = ModelChange.Create(existing.State, model, existing.PreviousEntity);
                changeSet.Remove(existing);
                changeSet.Add(change);
            }
            return changeSet;
        }
    }
}
