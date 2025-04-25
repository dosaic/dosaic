using Microsoft.EntityFrameworkCore.Storage;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Transactions
{
    public class EntityTransaction(IDbContextTransaction dbContextTransaction, Action? onComplete) : ITransaction
    {
        private bool _completeInvoked;

        private void Complete()
        {
            if (!_completeInvoked)
                onComplete?.Invoke();
            _completeInvoked = true;
        }

        public void Dispose()
        {
            dbContextTransaction.Dispose();
            Complete();
        }

        public async ValueTask DisposeAsync()
        {
            await dbContextTransaction.DisposeAsync();
            Complete();
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await dbContextTransaction.CommitAsync(cancellationToken);
            Complete();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await dbContextTransaction.RollbackAsync(cancellationToken);
            Complete();
        }
    }
}
