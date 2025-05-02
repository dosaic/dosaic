using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Transactions;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    /// <summary>
    /// Provider for entities
    /// </summary>
    public interface IEntityProvider
    {
        /// <summary>
        /// Begins a transaction
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a queryable for a specific entity with it needed domain logic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IQueryable<T> Get<T>() where T : class, IModel;

        /// <summary>
        /// Gets a queryable for the history of a specific entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IQueryable<History<T>> GetHistory<T>(NanoId id) where T : class, IModel, IHistory;

        /// <summary>
        /// Gets a queryable for the history of a entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IQueryable<History<T>> GetHistory<T>() where T : class, IModel, IHistory;

        /// <summary>
        /// Creates a new entity
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> CreateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IModel;

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IModel;

        /// <summary>
        /// Patches an existing entity
        /// </summary>
        /// <param name="id">The id of the entity</param>
        /// <param name="patchCommand">the patch command</param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> PatchAsync<T>(NanoId id, Action<T> patchCommand, CancellationToken cancellationToken = default)
            where T : class, IModel;

        /// <summary>
        /// Deletes an existing entity
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<T> DeleteAsync<T>(NanoId id, CancellationToken cancellationToken = default) where T : class, IModel;

        /// <summary>
        /// Batch adds, updates or deletes entities
        /// </summary>
        /// <param name="batch">The data for modification</param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task BatchAsync<T>(Batch<T> batch, CancellationToken cancellationToken = default) where T : class, IModel;
    }
}
