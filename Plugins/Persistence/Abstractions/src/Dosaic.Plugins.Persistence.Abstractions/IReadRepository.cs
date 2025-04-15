namespace Dosaic.Plugins.Persistence.Abstractions
{
    public interface IReadRepository<TEntity, TId>
        where TEntity : class, IIdentifier<TId>
    {
        Task<TEntity> FindByIdAsync(TId id, CancellationToken cancellationToken = default);
        Task<List<TEntity>> FindAsync(QueryOptions<TEntity> queryOptions, CancellationToken cancellationToken = default);
        Task<int> CountAsync(QueryOptions<TEntity> queryOptions, CancellationToken cancellationToken = default);
    }
}
