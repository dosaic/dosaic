using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{

    public class TestEfCoreDb(DbContextOptions<EfCoreDbContext> opts) : EfCoreDbContext(opts)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("public");
            // modelBuilder.MapDbEnums();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestEfCoreDb).Assembly);
            modelBuilder.ApplyHistories(typeof(TestUserModel));
            modelBuilder.ApplyEventSourcing(typeof(TestUserModel));
            modelBuilder.ApplyAuditFields(typeof(TestUserModel), typeof(TestUserModel));
            modelBuilder.ApplyKeys();
            modelBuilder.ApplySnakeCaseNamingConventions();
            // modelBuilder.ApplyEnumFields();

            base.OnModelCreating(modelBuilder);
        }
    }
}
