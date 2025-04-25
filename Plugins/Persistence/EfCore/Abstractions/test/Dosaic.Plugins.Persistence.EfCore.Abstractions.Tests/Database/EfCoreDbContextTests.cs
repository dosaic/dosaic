using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Database
{
    public class EfCoreDbContextTests
    {
        private TestEfCoreDb _db;
        private IServiceProvider _serviceProvider;
        private IServiceScope _serviceScope;
        private IServiceProvider _scopeServiceProvider;

        [SetUp]
        public void Setup()
        {
            _serviceProvider = Substitute.For<IServiceProvider>();
            _scopeServiceProvider = Substitute.For<IServiceProvider>();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
            _serviceScope = Substitute.For<IServiceScope>();
            _serviceScope.ServiceProvider.Returns(_scopeServiceProvider);
            scopeFactory.CreateScope().Returns(_serviceScope);
            var dbOpts = new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .UseApplicationServiceProvider(_serviceProvider);
            _db = new TestEfCoreDb(dbOpts.Options);
        }

        [TearDown]
        public void TearDown()
        {
            _serviceScope?.Dispose();
            _db?.Dispose();
        }

        [Test]
        public void GetReturnsDbSet()
        {
            var set = _db.Get<TestModel>();

            set.Should().NotBeNull();
            set.Should().BeAssignableTo<DbSet<TestModel>>();
        }

        [Test]
        public void GetQueryReturnsNoTrackingQueryable()
        {
            var query = _db.GetQuery<TestModel>();

            query.Should().NotBeNull();
        }

        [Test]
        public async Task SaveChangesAsyncWorksWithoutServiceScope()
        {
            var dbWithoutSp = new TestEfCoreDb(new DbContextOptionsBuilder<EfCoreDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);

            var testModel = TestModel.GetModel();
            dbWithoutSp.Set<TestModel>().Add(testModel);

            await dbWithoutSp.SaveChangesAsync();

            dbWithoutSp.Set<TestModel>().Should().Contain(testModel);
        }

        [Test]
        public async Task SaveChangesAsyncHandlesChangesAfterTriggers()
        {
            var afterTrigger = Substitute.For<IAfterTrigger<TestModel>>();
            afterTrigger.HandleAfterAsync(Arg.Any<ITriggerContext<TestModel>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask)
                .AndDoes(x => {
                    var context = x.Arg<ITriggerContext<TestModel>>();
                    var newEntity = TestModel.GetModel();
                    context.Database.Get<TestModel>().Add(newEntity);
                });

            _scopeServiceProvider.GetService(typeof(IEnumerable<IAfterTrigger<TestModel>>))
                .Returns(new[] { afterTrigger });

            var testModel = TestModel.GetModel();
            _db.Set<TestModel>().Add(testModel);

            await _db.SaveChangesAsync();

            _db.Set<TestModel>().Count().Should().Be(2);
        }
    }
}
