using AwesomeAssertions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
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

        [Test]
        public async Task BeforeTriggerExceptionPropagates()
        {
            var trigger = Substitute.For<IBeforeTrigger<TestModel>>();
            trigger.HandleBeforeAsync(Arg.Any<ITriggerContext<TestModel>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new InvalidOperationException("before-boom")));
            _scopeServiceProvider.GetService(typeof(IEnumerable<IBeforeTrigger<TestModel>>))
                .Returns(new[] { trigger });
            var interceptor = new SaveInterceptor(_scopeServiceProvider, _db);
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, TestModel.GetModel(), null)
            };
            var act = async () => await interceptor.BeforeSaveAsync(changeSet);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*before-boom*");
        }

        [Test]
        public async Task AfterTriggerExceptionPropagates()
        {
            var trigger = Substitute.For<IAfterTrigger<TestModel>>();
            trigger.HandleAfterAsync(Arg.Any<ITriggerContext<TestModel>>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new InvalidOperationException("after-boom")));
            _scopeServiceProvider.GetService(typeof(IEnumerable<IAfterTrigger<TestModel>>))
                .Returns(new[] { trigger });
            var interceptor = new SaveInterceptor(_scopeServiceProvider, _db);
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, TestModel.GetModel(), null)
            };
            var act = async () => await interceptor.AfterSaveAsync(changeSet);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*after-boom*");
        }

        [Test]
        public async Task AfterSaveHistoryWriterExceptionPropagates()
        {
            var writer = Substitute.For<IHistoryWriter>();
            writer.WriteAsync(Arg.Any<ChangeSet>(), Arg.Any<IDb>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new InvalidOperationException("writer-boom")));
            _scopeServiceProvider.GetService(typeof(IHistoryWriter)).Returns(writer);
            _scopeServiceProvider.GetService(typeof(IEnumerable<IAfterTrigger<TestModel>>)).Returns(null);
            var interceptor = new SaveInterceptor(_scopeServiceProvider, _db);
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, TestModel.GetModel(), null)
            };
            var act = async () => await interceptor.AfterSaveAsync(changeSet);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*writer-boom*");
        }

        [Test]
        public async Task GetTriggersReturnsEmptyWhenNoneRegistered()
        {
            _scopeServiceProvider.GetService(typeof(IEnumerable<IBeforeTrigger<TestModel>>)).Returns(null);
            _scopeServiceProvider.GetService(typeof(IEnumerable<IAfterTrigger<TestModel>>)).Returns(null);
            _scopeServiceProvider.GetService(typeof(IHistoryWriter)).Returns(null);
            var interceptor = new SaveInterceptor(_scopeServiceProvider, _db);
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, TestModel.GetModel(), null)
            };
            await interceptor.BeforeSaveAsync(changeSet);
            await interceptor.AfterSaveAsync(changeSet);
        }

        public class OrderTrackingTestModel : Model
        {
        }

        public class OrderTrackingState
        {
            public List<int> Order { get; } = new();
        }

        [TriggerOrder(Order = 2)]
        public class HighOrderBeforeTrigger(OrderTrackingState state) : IBeforeTrigger<OrderTrackingTestModel>
        {
            public Task HandleBeforeAsync(ITriggerContext<OrderTrackingTestModel> context, CancellationToken cancellationToken)
            {
                state.Order.Add(2);
                return Task.CompletedTask;
            }
        }

        [TriggerOrder(Order = 1)]
        public class LowOrderBeforeTrigger(OrderTrackingState state) : IBeforeTrigger<OrderTrackingTestModel>
        {
            public Task HandleBeforeAsync(ITriggerContext<OrderTrackingTestModel> context, CancellationToken cancellationToken)
            {
                state.Order.Add(1);
                return Task.CompletedTask;
            }
        }

        public class UnorderedBeforeTrigger(OrderTrackingState state) : IBeforeTrigger<OrderTrackingTestModel>
        {
            public Task HandleBeforeAsync(ITriggerContext<OrderTrackingTestModel> context, CancellationToken cancellationToken)
            {
                state.Order.Add(0);
                return Task.CompletedTask;
            }
        }

        [Test]
        public async Task TriggersAreOrderedByTriggerOrderAttribute()
        {
            var state = new OrderTrackingState();
            var triggers = new IBeforeTrigger<OrderTrackingTestModel>[]
            {
                new HighOrderBeforeTrigger(state),
                new UnorderedBeforeTrigger(state),
                new LowOrderBeforeTrigger(state)
            };
            _scopeServiceProvider.GetService(typeof(IEnumerable<IBeforeTrigger<OrderTrackingTestModel>>)).Returns(triggers);
            var interceptor = new SaveInterceptor(_scopeServiceProvider, _db);
            var changeSet = new ChangeSet
            {
                new ModelChange(ChangeState.Added, new OrderTrackingTestModel { Id = "X" }, null)
            };
            await interceptor.BeforeSaveAsync(changeSet);
            state.Order.Should().Equal(0, 1, 2);
        }
    }

}
