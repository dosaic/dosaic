namespace Dosaic.Plugins.Persistence.Abstractions
{
    public interface IRepository<TEntity, TId> : IReadRepository<TEntity, TId>
        where TEntity : class, IIdentifier<TId>
    {
        Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task RemoveAsync(TId id, CancellationToken cancellationToken = default);
    }
}
