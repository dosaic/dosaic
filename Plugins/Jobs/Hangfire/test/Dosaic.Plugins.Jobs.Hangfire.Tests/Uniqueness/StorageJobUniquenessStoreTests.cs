using AwesomeAssertions;
using Dosaic.Plugins.Jobs.Hangfire.Uniqueness;
using Hangfire;
using Hangfire.Storage;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Jobs.Hangfire.Tests.Uniqueness
{
    public class StorageJobUniquenessStoreTests
    {
        private const string SetKey = "dosaic:unique:test";
        private JobStorage _storage;
        private IStorageConnection _connection;
        private IWriteOnlyTransaction _transaction;
        private StorageJobUniquenessStore _store;

        [SetUp]
        public void Up()
        {
            _storage = Substitute.For<JobStorage>();
            _connection = Substitute.For<IStorageConnection>();
            _transaction = Substitute.For<IWriteOnlyTransaction>();
            _storage.GetConnection().Returns(_connection);
            _connection.CreateWriteTransaction().Returns(_transaction);
            _connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>())
                .Returns(Substitute.For<IDisposable>());
            _store = new StorageJobUniquenessStore(_storage);
        }

        [Test]
        public void FreeFingerprintsAreClaimedAndCommitted()
        {
            _connection.GetAllItemsFromSet(SetKey).Returns([]);
            var claim = new JobUniquenessClaim(SetKey, "abc", 100);

            _store.Claim([claim], 0).Should().Equal(claim);

            _transaction.Received(1).AddToSet(SetKey, "abc", 100);
            _transaction.Received(1).Commit();
        }

        [Test]
        public void TakenFingerprintsAreNotClaimed()
        {
            _connection.GetAllItemsFromSet(SetKey).Returns(["abc"]);

            _store.Claim([new JobUniquenessClaim(SetKey, "abc", 100)], 0).Should().BeEmpty();

            _transaction.DidNotReceiveWithAnyArgs().AddToSet(default, default, default);
            _transaction.DidNotReceive().Commit();
        }

        [Test]
        public void ADuplicateInsideOneCallOnlyWinsOnce()
        {
            _connection.GetAllItemsFromSet(SetKey).Returns([]);

            var owned = _store.Claim(
                [new JobUniquenessClaim(SetKey, "abc", 100), new JobUniquenessClaim(SetKey, "abc", 100)], 0);

            owned.Should().HaveCount(1);
            _transaction.Received(1).AddToSet(SetKey, "abc", 100);
        }

        [Test]
        public void EachSetIsLockedSeparately()
        {
            _connection.GetAllItemsFromSet(Arg.Any<string>()).Returns([]);

            _store.Claim([new JobUniquenessClaim(SetKey, "abc", 100),
                new JobUniquenessClaim("dosaic:unique:other", "abc", 100)], 0).Should().HaveCount(2);

            _connection.Received(1).AcquireDistributedLock($"{SetKey}:lock", Arg.Any<TimeSpan>());
            _connection.Received(1).AcquireDistributedLock("dosaic:unique:other:lock", Arg.Any<TimeSpan>());
        }

        [Test]
        public void EmptyClaimListsDoNotTouchTheStorage()
        {
            _store.Claim([], 0).Should().BeEmpty();
            _storage.DidNotReceive().GetConnection();
        }
    }
}
