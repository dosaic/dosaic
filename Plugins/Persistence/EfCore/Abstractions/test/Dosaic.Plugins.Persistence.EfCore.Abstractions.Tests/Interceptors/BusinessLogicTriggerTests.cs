using AutoBogus;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Triggers;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework.Utilities;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Interceptors
{
    public class BusinessLogicTriggerTests
    {
        private IBusinessLogicInterceptor _interceptor;
        private BusinessLogicTrigger<TestModel> _trigger;
        private ITriggerContext<TestModel> _triggerContext;
        private TestModel _entity;

        [SetUp]
        public void Setup()
        {
            _interceptor = Substitute.For<IBusinessLogicInterceptor>();
            _trigger = new BusinessLogicTrigger<TestModel>(_interceptor);
            _entity = AutoFaker.Generate<TestModel>(o => o.WithTreeDepth(1));
            _triggerContext = TestExtensions.GetTriggerContext(_entity, ChangeState.Added);
        }

        [Test]
        public async Task InterceptsBeforeSave()
        {
            await _trigger.HandleBeforeAsync(_triggerContext, CancellationToken.None);
            await _interceptor.Received(1)
                .InterceptBeforeAsync(_entity, ChangeState.Added, CancellationToken.None);
        }

        [Test]
        public async Task InterceptsAfterSave()
        {
            await _trigger.HandleAfterAsync(_triggerContext, CancellationToken.None);
            await _interceptor.Received(1)
                .InterceptAfterAsync(_entity, ChangeState.Added, CancellationToken.None);
        }
    }
}
