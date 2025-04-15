using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions
{
    public interface IDbContext<TEntity, TId> : IDbContext where TEntity : class, IIdentifier<TId>
    {
        DbSet<TEntity> GetSet();
    }

    public interface IDbContext
    {
        DbContext GetContext();

        void Migrate()
        {
            var context = GetContext();
            if (context.Database.IsRelational())
                context.Database.Migrate();
        }
    }
}
