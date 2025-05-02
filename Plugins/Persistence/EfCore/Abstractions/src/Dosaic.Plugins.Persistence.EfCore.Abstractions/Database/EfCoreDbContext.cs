using System.Diagnostics.CodeAnalysis;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public abstract class EfCoreDbContext(DbContextOptions options) : DbContext(options), IDb
    {
        protected readonly DbContextOptions Options = options;

        private IServiceScope GetServiceScope() => Options.GetExtension<CoreOptionsExtension>().ApplicationServiceProvider
            ?.GetRequiredService<IServiceScopeFactory>()?.CreateScope();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties(typeof(NanoId)).HaveConversion(typeof(NanoIdConverter));
            base.ConfigureConventions(configurationBuilder);
        }

        public DbSet<TEntity> Get<TEntity>() where TEntity : class, IModel
        {
            return Set<TEntity>();
        }

        public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IModel
        {
            return Set<TEntity>().AsExpandableEFCore(ExpressionOptimizer.visit).AsNoTracking();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = GetServiceScope();
            if (scope is null)
                return await base.SaveChangesAsync(cancellationToken);
            var sp = scope.ServiceProvider;
            var interceptor = new SaveInterceptor(sp, this);
            var changeSet = ChangeTracker.GetChangeSet();
            await interceptor.BeforeSaveAsync(changeSet, cancellationToken);
            changeSet = ChangeTracker.GetChangeSet();
            var result = await base.SaveChangesAsync(cancellationToken);
            changeSet = ChangeTracker.UpdateChangeSet(changeSet);
            await interceptor.AfterSaveAsync(changeSet, cancellationToken);
            if (ChangeTracker.HasChanges())
                await base.SaveChangesAsync(cancellationToken);
            return result;
        }

        [ExcludeFromCodeCoverage(Justification = "Only shorthand for interface")]
        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await Database.BeginTransactionAsync(cancellationToken);
            }
            catch (InvalidOperationException e)
            {
                if (e.Message.Contains("Microsoft.EntityFrameworkCore.Database.Transaction.TransactionIgnoredWarning"))
                {
                    return new NullTransaction();
                }
            }

            return await Database.BeginTransactionAsync(cancellationToken);
        }

        [ExcludeFromCodeCoverage(Justification = "Only for testing with in memory provider")]
        private class NullTransaction : IDbContextTransaction
        {
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public void Commit()
            {
            }

            public Task CommitAsync(CancellationToken cancellationToken = new()) => Task.CompletedTask;

            public void Rollback()
            {
            }

            public Task RollbackAsync(CancellationToken cancellationToken = new()) => Task.CompletedTask;

            public Guid TransactionId { get; } = Guid.NewGuid();
        }
    }
}
