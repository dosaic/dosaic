using Dosaic.Plugins.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EntityFramework
{
    public interface IDbContext<TEntity> : IDbContext where TEntity : class, IGuidIdentifier
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
