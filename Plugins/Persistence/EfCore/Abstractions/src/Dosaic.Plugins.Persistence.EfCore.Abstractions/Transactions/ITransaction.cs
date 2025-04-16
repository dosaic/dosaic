namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Transactions
{
    /// <summary>
    /// Specifies a transaction
    /// </summary>
    public interface ITransaction : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Commits the transaction
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rollbacks the transaction (default when disposed)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
