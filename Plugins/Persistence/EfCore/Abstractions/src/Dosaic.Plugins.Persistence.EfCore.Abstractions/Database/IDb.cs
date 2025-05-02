using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    /// <summary>
    /// The database to work
    /// </summary>
    public interface IDb : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Database model
        /// </summary>
        Microsoft.EntityFrameworkCore.Metadata.IModel Model { get; }

        /// <summary>
        /// Gets a <see cref="DbSet{TEntity}"/>
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        DbSet<TEntity> Get<TEntity>() where TEntity : class, IModel;

        /// <summary>
        /// Gets a <see cref="Queryable"/> of TEntity
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IModel;

        /// <summary>
        /// Saves the changes to the database
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a transaction on the context
        /// </summary>
        /// <returns></returns>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
