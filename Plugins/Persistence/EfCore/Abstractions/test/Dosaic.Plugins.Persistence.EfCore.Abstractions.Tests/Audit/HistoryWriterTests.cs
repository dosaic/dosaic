using AwesomeAssertions;
using Chronos.Abstractions;
using Dosaic.Extensions.NanoIds;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using EntityFrameworkCore.Testing.NSubstitute;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryWriterTests
    {
        private TestEfCoreDb _db;
        private IUserIdProvider _userIdProvider;
        private static readonly NanoId _userId = "User-Id";
        private IDateTimeProvider _dateTimeProvider;
        private HistoryWriter _writer;
        private static readonly DateTime _now = new(2020, 1, 1);

        [SetUp]
        public void Setup()
        {
            var dbOpts = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString());
            _db = Create.MockedDbContextFor<TestEfCoreDb>(dbOpts.Options);
            _dateTimeProvider = Substitute.For<IDateTimeProvider>();
            _dateTimeProvider.UtcNow.Returns(_now);
            _userIdProvider = Substitute.For<IUserIdProvider>();
            _userIdProvider.IsUserInteraction.Returns(true);
            _userIdProvider.UserId.Returns(_userId.Value);
            _userIdProvider.FallbackUserId.Returns("system");
            _writer = new HistoryWriter(_userIdProvider, _dateTimeProvider);
        }

        [TearDown]
        public void Down() => _db.Dispose();

        private History<TestHistoryModel> SingleHistoryEntry()
        {
            var entries = _db.ChangeTracker.Entries<History<TestHistoryModel>>().ToArray();
            entries.Should().HaveCount(1);
            return entries.Single().Entity;
        }

        [Test]
        public async Task EmptyChangeSetWritesNothing()
        {
            await _writer.WriteAsync(new ChangeSet(), _db, CancellationToken.None);
            _db.ChangeTracker.Entries<History<TestHistoryModel>>().Should().BeEmpty();
        }

        [Test]
        public async Task RootAddedProducesHistoryRow()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name" };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Added, entity, null) };
            _userIdProvider.IsUserInteraction.Returns(false);
            await _writer.WriteAsync(changeSet, _db, CancellationToken.None);
            var entry = SingleHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            entry.ModifiedBy.Should().Be((NanoId)"system");
            entry.ModifiedUtc.Should().Be(_now);
            var changes = entry.GetChanges();
            changes.Should().HaveCount(1);
            changes[nameof(TestHistoryModel.HistoryProperty)].Old.Should().BeNull();
            changes[nameof(TestHistoryModel.HistoryProperty)].New.Should().Be("Name");
        }

        [Test]
        public async Task RootModifiedExcludesAttributeIgnoredFields()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name1", Ignored = "X" };
            var unmodified = new TestHistoryModel { Id = "Id", HistoryProperty = "Name" };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Modified, entity, unmodified) };
            await _writer.WriteAsync(changeSet, _db, CancellationToken.None);
            var entry = SingleHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            entry.ModifiedBy.Should().Be(_userId);
            var changes = entry.GetChanges();
            changes.Should().HaveCount(1);
            changes[nameof(TestHistoryModel.HistoryProperty)].Old.Should().Be("Name");
            changes[nameof(TestHistoryModel.HistoryProperty)].New.Should().Be("Name1");
            changes.Should().NotContainKey(nameof(TestHistoryModel.Ignored));
        }

        [Test]
        public async Task RootDeletedEmitsOldValues()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name1" };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Deleted, entity, entity) };
            await _writer.WriteAsync(changeSet, _db, CancellationToken.None);
            var entry = SingleHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            var changes = entry.GetChanges();
            changes.Should().AllSatisfy(kv =>
            {
                kv.Value.Old.Should().NotBeNull();
                kv.Value.New.Should().BeNull();
            });
        }

        [Test]
        public async Task ChildWithoutResolverIsIgnored()
        {
            // No HistoryPathResolver passed -> children are silently skipped
            var child = new TestHistoryChildModel { Id = "C1", ParentId = "R1", ChildName = "Item", Price = 1m };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Added, child, null) };
            await _writer.WriteAsync(changeSet, _db, CancellationToken.None);
            _db.ChangeTracker.Entries<History<TestHistoryModel>>().Should().BeEmpty();
        }

        [Test]
        public async Task ChildBundledIntoRootRowWhenResolverPresent()
        {
            var resolver = HistoryPathResolver.Build([typeof(TestHistoryChildModel), typeof(TestHistoryGrandchildModel)]);
            var writer = new HistoryWriter(_userIdProvider, _dateTimeProvider, resolver);
            var root = new TestHistoryModel { Id = "R1", HistoryProperty = "alpha" };
            var child = new TestHistoryChildModel { Id = "C1", ParentId = "R1", ChildName = "Item", Price = 1m };
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, root, null),
                new ModelChange(ChangeState.Added, child, null)
            };
            await writer.WriteAsync(changeSet, _db, CancellationToken.None);
            var entry = SingleHistoryEntry();
            entry.ForeignId.Should().Be((NanoId)"R1");
            var changes = entry.GetChanges();
            changes.Should().ContainKey(nameof(TestHistoryModel.HistoryProperty));
            changes.Should().ContainKey("Children.C1");
        }

        [Test]
        public async Task RootWithZeroPathsAfterFilterSkipsPersist()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name", Ignored = "X" };
            var previous = new TestHistoryModel { Id = "Id", HistoryProperty = "Name", Ignored = null };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Modified, entity, previous) };
            await _writer.WriteAsync(changeSet, _db, CancellationToken.None);
            _db.ChangeTracker.Entries<History<TestHistoryModel>>().Should().BeEmpty();
        }

        [Test]
        public async Task PersistFailurePropagatesException()
        {
            var throwingDb = new ThrowingDb();
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name" };
            var changeSet = new ChangeSet { new ModelChange(ChangeState.Added, entity, null) };
            var act = async () => await _writer.WriteAsync(changeSet, throwingDb, CancellationToken.None);
            (await act.Should().ThrowAsync<System.Reflection.TargetInvocationException>())
                .WithInnerException<InvalidOperationException>().WithMessage("*persist-boom*");
        }

        private sealed class ThrowingDb : IDb
        {
            public Microsoft.EntityFrameworkCore.Metadata.IModel Model => throw new NotImplementedException();
            public DbSet<TEntity> Get<TEntity>() where TEntity : class, Dosaic.Plugins.Persistence.EfCore.Abstractions.Models.IModel
                => throw new InvalidOperationException("persist-boom");
            public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, Dosaic.Plugins.Persistence.EfCore.Abstractions.Models.IModel
                => throw new NotImplementedException();
            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
            public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public ValueTask DisposeAsync() => default;
            public void Dispose() { }
        }
    }
}
