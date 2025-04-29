// using System.Diagnostics.CodeAnalysis;
// using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Storage;
//
// namespace Dosaic.Plugins.Persistence.EfCore.NpgSql
// {
//     [ExcludeFromCodeCoverage(Justification = "Only wrapper for BeginTransactionAsync IsNpgsql logic")]
//     public abstract class NpgEfCoreDbContext(DbContextOptions opts) : EfCoreDbContext(opts)
//     {
//         [ExcludeFromCodeCoverage(Justification = "Only shorthand for interface")]
//         public override async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
//         {
//             return Database.IsNpgsql() ? await Database.BeginTransactionAsync(cancellationToken) : new NullTransaction();
//         }
//
//         [ExcludeFromCodeCoverage(Justification = "Only for testing with in memory provider")]
//         private class NullTransaction : IDbContextTransaction
//         {
//             public void Dispose() { }
//
//             public ValueTask DisposeAsync() => ValueTask.CompletedTask;
//
//             public void Commit() { }
//
//             public Task CommitAsync(CancellationToken cancellationToken = new()) => Task.CompletedTask;
//
//             public void Rollback() { }
//
//             public Task RollbackAsync(CancellationToken cancellationToken = new()) => Task.CompletedTask;
//
//             public Guid TransactionId { get; } = Guid.NewGuid();
//         }
//     }
// }
