using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    public static class DbSetHistoryExtensions
    {
        /// <summary>
        /// Reconstructs <typeparamref name="TRoot"/> with id <paramref name="id"/> at the state it had at
        /// <paramref name="date"/> by replaying its <see cref="History{TRoot}"/> rows up to that point.
        /// Returns <c>null</c> when no creation row exists at or before the date.
        /// </summary>
        public static Task<TRoot> LoadFromHistoryAsync<TRoot>(this DbSet<TRoot> set, NanoId id, DateTime date,
            CancellationToken cancellationToken = default)
            where TRoot : class, IModel, IHistory =>
            set.LoadFromHistoryAsync(id, date, HistoryReplayOptions.Default, cancellationToken);

        /// <summary>
        /// Same as <see cref="LoadFromHistoryAsync{TRoot}(DbSet{TRoot}, NanoId, DateTime, CancellationToken)"/> but
        /// replays with the supplied <paramref name="options"/> (e.g. an app-specific
        /// <see cref="HistoryReplayOptions.ValueConverter"/> for legacy change-set values).
        /// </summary>
        public static async Task<TRoot> LoadFromHistoryAsync<TRoot>(this DbSet<TRoot> set, NanoId id, DateTime date,
            HistoryReplayOptions options, CancellationToken cancellationToken = default)
            where TRoot : class, IModel, IHistory
        {
            var context = set.GetService<ICurrentDbContext>().Context;
            var rows = await context.Set<History<TRoot>>()
                .AsNoTracking()
                .Where(h => h.ForeignId == id && h.ModifiedUtc <= date)
                .OrderBy(h => h.ModifiedUtc)
                .ThenBy(h => h.Id)
                .Select(h => h.ChangeSet)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) return null;
            return HistoryReplay.Replay<TRoot>(id, rows.Select(ObjectChanges.FromJson), options);
        }

        /// <summary>
        /// Returns the complete history timeline for <typeparamref name="TRoot"/> with id <paramref name="id"/>
        /// as a sequence of (timestamp, snapshot) pairs, one per recorded save event.
        /// </summary>
        public static Task<IReadOnlyList<HistoryTimelineEntry<TRoot>>> LoadHistoryTimelineAsync<TRoot>(
            this DbSet<TRoot> set, NanoId id, CancellationToken cancellationToken = default)
            where TRoot : class, IModel, IHistory =>
            set.LoadHistoryTimelineAsync(id, HistoryReplayOptions.Default, cancellationToken);

        /// <summary>
        /// Same as <see cref="LoadHistoryTimelineAsync{TRoot}(DbSet{TRoot}, NanoId, CancellationToken)"/> but
        /// replays with the supplied <paramref name="options"/>.
        /// </summary>
        public static async Task<IReadOnlyList<HistoryTimelineEntry<TRoot>>> LoadHistoryTimelineAsync<TRoot>(
            this DbSet<TRoot> set, NanoId id, HistoryReplayOptions options, CancellationToken cancellationToken = default)
            where TRoot : class, IModel, IHistory
        {
            var context = set.GetService<ICurrentDbContext>().Context;
            var rows = await context.Set<History<TRoot>>()
                .AsNoTracking()
                .Where(h => h.ForeignId == id)
                .OrderBy(h => h.ModifiedUtc)
                .ThenBy(h => h.Id)
                .Select(h => new { h.ModifiedUtc, h.ChangeSet })
                .ToListAsync(cancellationToken);
            var timeline = new List<HistoryTimelineEntry<TRoot>>(rows.Count);
            var accumulated = new List<ObjectChanges>(rows.Count);
            foreach (var row in rows)
            {
                accumulated.Add(ObjectChanges.FromJson(row.ChangeSet));
                var snapshot = HistoryReplay.Replay<TRoot>(id, accumulated, options);
                timeline.Add(new HistoryTimelineEntry<TRoot>(row.ModifiedUtc, snapshot));
            }
            return timeline;
        }
    }

    public sealed record HistoryTimelineEntry<TRoot>(DateTime ModifiedUtc, TRoot Snapshot)
        where TRoot : class, IModel, IHistory;
}
