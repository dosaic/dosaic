using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Interceptors
{
    public class SaveInterceptorTests
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
        public async Task SaveChangesAsyncWorksWithoutTriggers()
        {
            _scopeServiceProvider.GetService(typeof(IEnumerable<IBeforeTrigger<TestModel>>))
                .Returns(null);
            var testModel = TestModel.GetModel();
            _db.Set<TestModel>().Add(testModel);
            await _db.SaveChangesAsync();
        }

        [Test]
        public async Task SaveChangesAsyncCallsBeforeTrigger()
        {
            var trigger = Substitute.For<IBeforeTrigger<TestModel>>();
            _scopeServiceProvider.GetService(typeof(IEnumerable<IBeforeTrigger<TestModel>>))
                .Returns(new[] { trigger });
            var testModel = TestModel.GetModel();
            _db.Set<TestModel>().Add(testModel);
            await _db.SaveChangesAsync();
            await trigger.Received(1)
                .HandleBeforeAsync(
                    Arg.Is<ITriggerContext<TestModel>>(i =>
                        i.Database == _db
                        && i.ChangeSet.Count == 1
                        && i.ChangeSet[0].Entity == testModel),
                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SaveChangesAsyncCallsAfterTrigger()
        {
            var trigger = Substitute.For<IAfterTrigger<TestModel>>();
            _scopeServiceProvider.GetService(typeof(IEnumerable<IAfterTrigger<TestModel>>))
                .Returns(new[] { trigger });
            var testModel = TestModel.GetModel();
            _db.Set<TestModel>().Add(testModel);
            await _db.SaveChangesAsync();
            await trigger.Received(1)
                .HandleAfterAsync(
                    Arg.Is<ITriggerContext<TestModel>>(i =>
                        i.Database == _db
                        && i.ChangeSet.Count == 1
                        && i.ChangeSet[0].Entity == testModel),
                    Arg.Any<CancellationToken>());
        }
    }

    public class TestEfCoreDb(DbContextOptions<EfCoreDbContext> opts) : EfCoreDbContext(opts)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("public");
            // modelBuilder.MapDbEnums();
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestEfCoreDb).Assembly);
            // modelBuilder.ApplyHistories();
            // modelBuilder.ApplyEventSourcing();
            // modelBuilder.ApplyAuditFields();
            // modelBuilder.ApplyKeasyModels();
            modelBuilder.ApplyKeys();
            // modelBuilder.ApplyNamingConventions();
            // modelBuilder.ApplyEnumFields();

            base.OnModelCreating(modelBuilder);
        }
    }
}
