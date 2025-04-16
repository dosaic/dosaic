using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests
{
    internal record TestEntity(Guid Id, string Name, DateTime CreationDate)
    {
        public Guid Id { get; set; } = Id;

        public DateTime CreationDate { get; set; } = CreationDate;
    }

    internal class TestContext : DbContext
    {
        public TestContext(DbContextOptions<TestContext> options) : base(options) { }

        public DbContext GetContext() => this;

        public DbSet<TestEntity> GetSet() => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).IsRequired();
                e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
