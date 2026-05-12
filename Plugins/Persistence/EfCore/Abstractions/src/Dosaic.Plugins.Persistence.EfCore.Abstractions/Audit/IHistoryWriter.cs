using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// Captures bundled <see cref="History{T}"/> rows for every <see cref="IHistory"/> root affected by a save —
    /// including roots only touched via <see cref="HistoryParentAttribute"/>-tagged children. Invoked by
    /// <see cref="Interceptors.SaveInterceptor"/> after entity triggers run.
    /// </summary>
    public interface IHistoryWriter
    {
        Task WriteAsync(ChangeSet changeSet, IDb db, CancellationToken cancellationToken = default);
    }
}
