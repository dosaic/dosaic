using Chronos.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Identifiers;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using FluentAssertions;
using EntityFrameworkCore.Testing.NSubstitute;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Audit
{
    public class HistoryTriggerTests
    {
        private TestEfCoreDb _db;
        private IUserProvider _userProvider;
        private static readonly NanoId _userId = "User-Id";
        private IDateTimeProvider _dateTimeProvider;
        private HistoryTrigger<TestHistoryModel> _trigger;
        private static readonly DateTime _now = new(2020, 1, 1);

        [SetUp]
        public void Setup()
        {
            var dbOpts = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString());
            _db = Create.MockedDbContextFor<TestEfCoreDb>(dbOpts.Options);
            _dateTimeProvider = Substitute.For<IDateTimeProvider>();
            _dateTimeProvider.UtcNow.Returns(_now);
            _userProvider = Substitute.For<IUserProvider>();
            _userProvider.IsUserInteraction.Returns(true);
            _userProvider.UserId.Returns(_userId.Value);
            _userProvider.FallbackUserId.Returns("system");
            _trigger = new HistoryTrigger<TestHistoryModel>(_userProvider, _dateTimeProvider);
        }

        private ITriggerContext<TestHistoryModel> GetContext(TestHistoryModel entity, ChangeState changeState, TestHistoryModel unmodified = null)
        {
            var context = Substitute.For<ITriggerContext<TestHistoryModel>>();
            var changeSet = new ChangeSet<TestHistoryModel> { new ModelChange<TestHistoryModel>(changeState, entity, unmodified) };
            context.ChangeSet.Returns(changeSet);
            context.Database.Returns(_db);
            return context;
        }

        [TearDown]
        public void Down()
        {
            _db.Dispose();
        }

        private History<TestHistoryModel> GetHistoryEntry()
        {
            var history = _db.ChangeTracker.Entries<History<TestHistoryModel>>().ToArray();
            history.Should().HaveCount(1);
            var entry = history.Single().Entity;


            return entry;
        }

        [Test]
        public async Task CanHandleAddChanges()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name" };
            var context = GetContext(entity, ChangeState.Added);
            _userProvider.IsUserInteraction.Returns(false);
            await _trigger.HandleAfterAsync(context, CancellationToken.None);
            var entry = GetHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            entry.ModifiedBy.Should().Be("system");
            entry.ModifiedUtc.Should().Be(_now);
            entry.State.Should().Be(ChangeState.Added);

            var changeSet = entry.GetChanges();
            changeSet.Should().NotBeNullOrEmpty();
            changeSet.Should().HaveCount(1);
            changeSet[nameof(TestHistoryModel.HistoryProperty)].Old.Should().BeNull();
            changeSet[nameof(TestHistoryModel.HistoryProperty)].New.Should().Be("Name");
        }

        [Test]
        public async Task CanHandleUpdateChanges()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name1", Ignored = "123" };
            var unmodified = new TestHistoryModel { Id = "Id", HistoryProperty = "Name" };
            var context = GetContext(entity, ChangeState.Modified, unmodified);
            await _trigger.HandleAfterAsync(context, CancellationToken.None);
            var entry = GetHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            entry.ModifiedBy.Should().Be(_userId);
            entry.ModifiedUtc.Should().Be(_now);
            entry.State.Should().Be(ChangeState.Modified);
            var changeSet = entry.GetChanges();
            changeSet.Should().NotBeNullOrEmpty();
            changeSet.Should().HaveCount(1);
            changeSet[nameof(TestHistoryModel.HistoryProperty)].Old.Should().Be("Name");
            changeSet[nameof(TestHistoryModel.HistoryProperty)].New.Should().Be("Name1");
        }

        [Test]
        public async Task CanHandleDeleteChanges()
        {
            var entity = new TestHistoryModel { Id = "Id", HistoryProperty = "Name1" };
            var context = GetContext(entity, ChangeState.Deleted, entity);
            await _trigger.HandleAfterAsync(context, CancellationToken.None);
            var entry = GetHistoryEntry();
            entry.ForeignId.Should().Be(entity.Id);
            entry.ModifiedBy.Should().Be(_userId);
            entry.ModifiedUtc.Should().Be(_now);
            entry.State.Should().Be(ChangeState.Deleted);
            var changeSet = entry.GetChanges();
            changeSet.Should().AllSatisfy(e =>
            {
                e.Value.Old.Should().NotBeNull();
                e.Value.New.Should().BeNull();
            });
        }
    }
}
