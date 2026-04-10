using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    using System;
    using System.Linq;
    using AwesomeAssertions;
    using NUnit.Framework;

    [Explicit]
    public class DbExtensionsGraphPostgresTests
    {
        public class PostgresTestDb(DbContextOptions<EfCoreDbContext> options) : EfCoreDbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.HasDefaultSchema("public");

                modelBuilder.Entity<TestAuditModel>(b =>
                {
                    b.ToTable("test_audit_model");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasMaxLength(10);
                    b.Property(x => x.Name);
                    b.HasMany(x => x.Subs).WithOne();
                });

                modelBuilder.Entity<SubTestModel>(b =>
                {
                    b.ToTable("sub_test_model");
                    b.HasKey(x => x.Id);
                    b.Property(x => x.Id).HasMaxLength(10);
                    b.Property(x => x.DeepName);
                    b.OwnsOne(x => x.OwnedInfo, owned =>
                    {
                        owned.Property(x => x.InfoKey).HasColumnName("owned_info_key");
                        owned.Property(x => x.InfoValue).HasColumnName("owned_info_value");
                    });
                });

                base.OnModelCreating(modelBuilder);
            }
        }

        private PostgresTestDb _db;

        [SetUp]
        public void Up()
        {
            var options = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=dosaic_test;Username=postgres;Password=postgres")
                .Options;
            _db = new PostgresTestDb(options);
            _db.Database.EnsureDeleted();
            _db.Database.EnsureCreated();
        }

        [TearDown]
        public void Down()
        {
            _db?.Database.EnsureDeleted();
            _db?.Dispose();
        }

        private static TestAuditModel GetModelWithOwnedInfo() => new()
        {
            Id = "1",
            Name = "test",
            Subs =
            [
                new SubTestModel
                {
                    Id = "11",
                    DeepName = "11",
                    OwnedInfo = new SubTestOwnedInfo { InfoKey = "key1", InfoValue = "val1" }
                },
                new SubTestModel
                {
                    Id = "12",
                    DeepName = "12",
                    OwnedInfo = new SubTestOwnedInfo { InfoKey = "key2", InfoValue = "val2" }
                }
            ],
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "test",
            ModifiedBy = "test"
        };

        [Test]
        public async Task UpdateGraphCanInsertWithOwnedEntities()
        {
            var model = GetModelWithOwnedInfo();
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();

            _db.ChangeTracker.Clear();
            var loaded = await _db.Get<TestAuditModel>()
                .Include(x => x.Subs)
                .SingleAsync(x => x.Id == "1");
            loaded.Subs.Should().HaveCount(2);
            loaded.Subs.Single(s => s.Id == "11").OwnedInfo.InfoKey.Should().Be("key1");
            loaded.Subs.Single(s => s.Id == "12").OwnedInfo.InfoKey.Should().Be("key2");
        }

        [Test]
        public async Task UpdateGraphCanModifyOwnedEntityOnSub()
        {
            var model = GetModelWithOwnedInfo();
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            model.Subs.Single(s => s.Id == "11").OwnedInfo =
                new SubTestOwnedInfo { InfoKey = "key1-updated", InfoValue = "val1-updated" };

            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();

            _db.ChangeTracker.Clear();
            var loaded = await _db.Get<SubTestModel>().SingleAsync(x => x.Id == "11");
            loaded.OwnedInfo.InfoKey.Should().Be("key1-updated");
            loaded.OwnedInfo.InfoValue.Should().Be("val1-updated");
        }

        [Test]
        public async Task UpdateGraphCanModifyOwnedEntityFromNullToValue()
        {
            var model = new TestAuditModel
            {
                Id = "1",
                Name = "test",
                Subs =
                [
                    new SubTestModel { Id = "11", DeepName = "11", OwnedInfo = null }
                ],
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = "test",
                ModifiedBy = "test"
            };
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            model.Subs.Single().OwnedInfo = new SubTestOwnedInfo { InfoKey = "newKey", InfoValue = "newVal" };

            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();

            _db.ChangeTracker.Clear();
            var loaded = await _db.Get<SubTestModel>().SingleAsync(x => x.Id == "11");
            loaded.OwnedInfo.InfoKey.Should().Be("newKey");
            loaded.OwnedInfo.InfoValue.Should().Be("newVal");
        }

        [Test]
        public async Task UpdateGraphCanAddAndRemoveSubsWithOwnedEntities()
        {
            var model = GetModelWithOwnedInfo();
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            model.Subs = [
                model.Subs.Single(s => s.Id == "11"),
                new SubTestModel
                {
                    Id = "13",
                    DeepName = "13",
                    OwnedInfo = new SubTestOwnedInfo { InfoKey = "key3", InfoValue = "val3" }
                }
            ];

            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();

            _db.ChangeTracker.Clear();
            var loaded = await _db.Get<TestAuditModel>()
                .Include(x => x.Subs)
                .SingleAsync(x => x.Id == "1");
            loaded.Subs.Should().HaveCount(2);
            loaded.Subs.Should().Contain(s => s.Id == "11");
            loaded.Subs.Should().Contain(s => s.Id == "13");
            loaded.Subs.Should().NotContain(s => s.Id == "12");
            loaded.Subs.Single(s => s.Id == "13").OwnedInfo.InfoKey.Should().Be("key3");
        }

        [Test]
        public async Task UpdateGraphPreservesUnchangedOwnedEntity()
        {
            var model = GetModelWithOwnedInfo();
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            model.Subs.Single(s => s.Id == "11").DeepName = "11-updated";

            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();

            _db.ChangeTracker.Clear();
            var loaded = await _db.Get<SubTestModel>().SingleAsync(x => x.Id == "11");
            loaded.DeepName.Should().Be("11-updated");
            loaded.OwnedInfo.InfoKey.Should().Be("key1");
            loaded.OwnedInfo.InfoValue.Should().Be("val1");
        }

        [Test]
        public async Task UpdateGraphMultipleRoundTripsWithOwnedEntities()
        {
            var model = GetModelWithOwnedInfo();
            _db.Add(model);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            model.Subs.Single(s => s.Id == "11").OwnedInfo =
                new SubTestOwnedInfo { InfoKey = "round1", InfoValue = "round1" };
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            var loaded = await _db.Get<SubTestModel>().SingleAsync(x => x.Id == "11");
            loaded.OwnedInfo.InfoKey.Should().Be("round1");

            model.Subs.Single(s => s.Id == "11").OwnedInfo =
                new SubTestOwnedInfo { InfoKey = "round2", InfoValue = "round2" };
            await _db.UpdateGraphAsync(model, m => m.Id == model.Id);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            loaded = await _db.Get<SubTestModel>().SingleAsync(x => x.Id == "11");
            loaded.OwnedInfo.InfoKey.Should().Be("round2");
        }
    }
}
