using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{

    using System;
    using System.Linq;
    using FluentAssertions;
    using NUnit.Framework;

    namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
    {
    }

    public class DbExtensionsGraphTests
    {

        public class TestEfDb(DbContextOptions<EfCoreDbContext> opts) : TestEfCoreDb(opts)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestAuditModel>().HasKey(x => x.Id);
                modelBuilder.Entity<SubTestModel>().HasKey(x => x.Id);
            }
        }

        private TestEfCoreDb _db;

        [SetUp]
        public void Up()
        {

            _db = new TestEfCoreDb(new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        [TearDown]
        public void Down()
        {
            _db.Dispose();
        }

        private static TestAuditModel GetModel() => new TestAuditModel
        {
            Id = "1",
            Name = "test",
            Subs =
            [
                new SubTestModel { Id = "11", DeepName = "11" }, new SubTestModel { Id = "12", DeepName = "12" }
            ],
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "test"
        };

        [Test]
        public async Task UpdateGraphCanAddEverything()
        {
            var model = GetModel();
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            _db.ChangeTracker.Entries().Should().AllSatisfy(x => x.State.Should().Be(EntityState.Added));
            await _db.SaveChangesAsync();
        }

        [Test]
        public async Task UpdateGraphCanModify()
        {
            var model = GetModel();
            model.ModifiedBy = "test";
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.Entry(model).State = EntityState.Detached;
            model.Subs.First().DeepName = "Changed";
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            _db.ChangeTracker.Entries<TestAuditModel>().Single().State.Should().Be(EntityState.Modified);
            _db.ChangeTracker.Entries<SubTestModel>().Should().AllSatisfy(x => x.State.Should().Be(EntityState.Modified));
            model.ModifiedUtc.Should().BeWithin(TimeSpan.FromSeconds(1));
            model.ModifiedBy.Should().Be("test");
            model.Subs.Add(new SubTestModel { Id = "33", DeepName = "33" });
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            _db.ChangeTracker.Entries<SubTestModel>().Should().Contain(x => x.State == EntityState.Added);

        }

        [Test]
        public async Task UpdateGraphCanDelete()
        {
            var model = GetModel();
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.Entry(model).State = EntityState.Detached;
            model.Subs = [model.Subs.First()];
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            _db.ChangeTracker.Entries<TestAuditModel>().Single().State.Should().Be(EntityState.Modified);
            _db.ChangeTracker.Entries<SubTestModel>().Should().Contain(x => x.State == EntityState.Deleted);
            _db.ChangeTracker.Entries<SubTestModel>().Should().Contain(x => x.State == EntityState.Modified);
        }
    }
}
