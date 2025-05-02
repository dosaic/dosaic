using Dosaic.Plugins.Persistence.EfCore.Abstractions.Transactions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Transactions
{
    public class EntityTransactionTests
    {
        private IDbContextTransaction _mockDbContextTransaction;
        private Action _mockOnComplete;

        [SetUp]
        public void Setup()
        {
            _mockDbContextTransaction = Substitute.For<IDbContextTransaction>();
            _mockOnComplete = Substitute.For<Action>();
        }

        [Test]
        public async Task CommitAsyncCallsUnderlyingTransactionCommit()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.CommitAsync();

            await _mockDbContextTransaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CommitAsyncInvokesOnCompleteCallback()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.CommitAsync();

            _mockOnComplete.Received(1).Invoke();
        }

        [Test]
        public async Task RollbackAsyncCallsUnderlyingTransactionRollback()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.RollbackAsync();

            await _mockDbContextTransaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RollbackAsyncInvokesOnCompleteCallback()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.RollbackAsync();

            _mockOnComplete.Received(1).Invoke();
        }

        [Test]
        public void DisposeCallsUnderlyingTransactionDispose()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            transaction.Dispose();

            _mockDbContextTransaction.Received(1).Dispose();
        }

        [Test]
        public void DisposeInvokesOnCompleteCallback()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            transaction.Dispose();

            _mockOnComplete.Received(1).Invoke();
        }

        [Test]
        public async Task DisposeAsyncCallsUnderlyingTransactionDisposeAsync()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.DisposeAsync();

            await _mockDbContextTransaction.Received(1).DisposeAsync();
        }

        [Test]
        public async Task DisposeAsyncInvokesOnCompleteCallback()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.DisposeAsync();

            _mockOnComplete.Received(1).Invoke();
        }

        [Test]
        public async Task OnCompleteIsOnlyCalledOnceEvenWhenMultipleOperationsArePerformed()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, _mockOnComplete);

            await transaction.CommitAsync();
            await transaction.RollbackAsync();
            transaction.Dispose();

            _mockOnComplete.Received(1).Invoke();
        }

        [Test]
        public void NullOnCompleteCallbackDoesNotThrowException()
        {
            var transaction = new EntityTransaction(_mockDbContextTransaction, null);

            Action act = () => transaction.Dispose();

            act.Should().NotThrow();
        }
    }
}
