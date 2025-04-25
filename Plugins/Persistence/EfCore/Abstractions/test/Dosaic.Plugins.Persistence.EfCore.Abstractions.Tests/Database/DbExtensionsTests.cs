// using Chronos.Abstractions;
// using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
// using Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Eventsourcing;
// using FluentAssertions;
// using Microsoft.EntityFrameworkCore;
// using NSubstitute;
// using NUnit.Framework;
//
// namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
// {
//     public class DbExtensionsTests
//     {
//         private IDb _db;
//         private IDateTimeProvider _dateTimeProvider;
//         private List<TestAggregate> _testEvents;
//         private DbSet<TestAggregate> _dbSet;
//
//         [SetUp]
//         public void Setup()
//         {
//             _dateTimeProvider = Substitute.For<IDateTimeProvider>();
//             _dateTimeProvider.UtcNow.Returns(new DateTime(2023, 1, 1));
//
//             _testEvents = new List<TestAggregate>
//             {
//                 new TestAggregate
//                 {
//                     Id = "1",
//                     EventData = "test",
//                     EventType = TestEventType.Create,
//                     GroupId = "A",
//                     IsDeleted = false,
//                     ValidFrom = new DateTime(2022, 1, 1)
//                 },
//                 new TestAggregate
//                 {
//                     Id = "2",
//                     EventData = "test",
//                     EventType = TestEventType.Create,
//                     GroupId = "A",
//                     IsDeleted = false,
//                     ValidFrom = new DateTime(2022, 6, 1)
//                 },
//                 new TestAggregate
//                 {
//                     Id = "3",
//                     EventData = "test",
//                     EventType = TestEventType.Create,
//                     GroupId = "B",
//                     IsDeleted = false,
//                     ValidFrom = new DateTime(2022, 1, 1)
//                 },
//                 new TestAggregate
//                 {
//                     Id = "4",
//                     EventData = "test",
//                     EventType = TestEventType.Create,
//                     GroupId = "A",
//                     IsDeleted = true,
//                     ValidFrom = new DateTime(2022, 1, 1)
//                 },
//                 new TestAggregate
//                 {
//                     Id = "5",
//                     EventData = "test",
//                     EventType = TestEventType.Create,
//                     GroupId = "A",
//                     IsDeleted = false,
//                     ValidFrom = new DateTime(2023, 2, 1)
//                 }
//             };
//
//             _dbSet = MockDbSet(_testEvents);
//
//             _db = Substitute.For<IDb>();
//             _db.GetQuery<TestAggregate>().Returns(_testEvents.Where(x => true).AsQueryable());
//         }
//
//         [Test]
//         public async Task GetEventsReturnsMatchingNonDeletedValidEvents()
//         {
//             var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");
//
//             var result = await _db.GetEvents(aggregate, _dateTimeProvider);
//
//             result.Should().HaveCount(2);
//             result.Should().Contain(x => x.Id == "1");
//             result.Should().Contain(x => x.Id == "2");
//         }
//
//         [Test]
//         public async Task GetEventsExcludesDeletedEvents()
//         {
//             var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");
//
//             var result = await _db.GetEvents(aggregate, _dateTimeProvider);
//
//             result.Should().NotContain(x => x.Id == "4");
//         }
//
//         [Test]
//         public async Task GetEventsExcludesFutureEvents()
//         {
//             var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");
//
//             var result = await _db.GetEvents(aggregate, _dateTimeProvider);
//
//             result.Should().NotContain(x => x.Id == "5");
//         }
//
//         [Test]
//         public async Task GetEventsReturnsEmptyArrayWhenNoMatches()
//         {
//             var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "C");
//
//             var result = await _db.GetEvents(aggregate, _dateTimeProvider);
//
//             result.Should().BeEmpty();
//         }
//
//         [Test]
//         public async Task GetEventsReturnsAllEventsWhenNoEventMatcherAttributesExist()
//         {
//             var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "X");
//             _db.GetQuery<NoMatcherAggregateEvent>().Returns(new List<NoMatcherAggregateEvent>
//             {
//                 new NoMatcherAggregateEvent
//                 {
//                     Id = "1",
//                     EventData = "test",
//                     GroupId = "Y",
//                     IsDeleted = false,
//                     ValidFrom = new DateTime(2022, 1, 1)
//                 }
//             }.AsQueryable());
//
//             var result = await _db.GetEvents(aggregate, _dateTimeProvider);
//
//             result.Should().HaveCount(1);
//         }
//
//         private static DbSet<T> MockDbSet<T>(List<T> data) where T : class
//         {
//             var queryable = data.AsQueryable();
//             var dbSet = Substitute.For<DbSet<T>, IQueryable<T>>();
//             ((IQueryable<T>)dbSet).Provider.Returns(queryable.Provider);
//             ((IQueryable<T>)dbSet).Expression.Returns(queryable.Expression);
//             ((IQueryable<T>)dbSet).ElementType.Returns(queryable.ElementType);
//             ((IQueryable<T>)dbSet).GetEnumerator().Returns(queryable.GetEnumerator());
//             return dbSet;
//         }
//     }
// }
//
//


using Chronos.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Eventsourcing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    public class DbExtensionsTests
    {
        private TestEfCoreDb _db;
        private IDateTimeProvider _dateTimeProvider;

        [SetUp]
        public void Setup()
        {
            _dateTimeProvider = Substitute.For<IDateTimeProvider>();
            _dateTimeProvider.UtcNow.Returns(new DateTime(2023, 1, 1));

            var options = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            _db = new TestEfCoreDb(options);

            // Seed test data
            _db.AddRange(
                new TestAggregate
                {
                    Id = "1",
                    EventData = "test",
                    EventType = TestEventType.Create,
                    GroupId = "A",
                    IsDeleted = false,
                    ValidFrom = new DateTime(2022, 1, 1)
                },
                new TestAggregate
                {
                    Id = "2",
                    EventData = "test",
                    EventType = TestEventType.Create,
                    GroupId = "A",
                    IsDeleted = false,
                    ValidFrom = new DateTime(2022, 6, 1)
                },
                new TestAggregate
                {
                    Id = "3",
                    EventData = "test",
                    EventType = TestEventType.Create,
                    GroupId = "B",
                    IsDeleted = false,
                    ValidFrom = new DateTime(2022, 1, 1)
                },
                new TestAggregate
                {
                    Id = "4",
                    EventData = "test",
                    EventType = TestEventType.Create,
                    GroupId = "A",
                    IsDeleted = true,
                    ValidFrom = new DateTime(2022, 1, 1)
                },
                new TestAggregate
                {
                    Id = "5",
                    EventData = "test",
                    EventType = TestEventType.Create,
                    GroupId = "A",
                    IsDeleted = false,
                    ValidFrom = new DateTime(2023, 2, 1)
                }
            );

            _db.AddRange(
                new NoMatcherAggregateEvent
                {
                    Id = "1",
                    EventData = "test",
                    GroupId = "Y",
                    IsDeleted = false,
                    ValidFrom = new DateTime(2022, 1, 1)
                }
            );

            _db.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            _db?.Dispose();
        }

        [Test]
        public async Task GetEventsReturnsMatchingNonDeletedValidEvents()
        {
            var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");

            var result = await _db.GetEvents(aggregate, _dateTimeProvider);

            result.Should().HaveCount(2);
            result.Should().Contain(x => x.Id == "1");
            result.Should().Contain(x => x.Id == "2");
        }

        [Test]
        public async Task GetEventsExcludesDeletedEvents()
        {
            var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");

            var result = await _db.GetEvents(aggregate, _dateTimeProvider);

            result.Should().NotContain(x => x.Id == "4");
        }

        [Test]
        public async Task GetEventsExcludesFutureEvents()
        {
            var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "A");

            var result = await _db.GetEvents(aggregate, _dateTimeProvider);

            result.Should().NotContain(x => x.Id == "5");
        }

        [Test]
        public async Task GetEventsReturnsEmptyArrayWhenNoMatches()
        {
            var aggregate = TestAggregate.Init(_dateTimeProvider.UtcNow, "C");

            var result = await _db.GetEvents(aggregate, _dateTimeProvider);

            result.Should().BeEmpty();
        }

        [Test]
        public async Task GetEventsReturnsAllEventsWhenNoEventMatcherAttributesExist()
        {
            var aggregate = new NoMatcherAggregateEvent
            {
                Id = "1", EventData = "test", ValidFrom = _dateTimeProvider.UtcNow, GroupId = "X"
            };

            var result = await _db.GetEvents(aggregate, _dateTimeProvider);

            result.Should().HaveCount(1);
        }
    }
}
