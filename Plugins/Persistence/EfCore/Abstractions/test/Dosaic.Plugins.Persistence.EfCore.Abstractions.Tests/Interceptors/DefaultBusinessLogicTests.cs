using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Interceptors;
using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Tests.Interceptors
{
    public class DefaultBusinessLogicTests
    {
        private IBusinessLogic<TestModel> _businessLogic;

        [SetUp]
        public void SetUp()
        {
            _businessLogic = new TestBusinessLogic();
        }

        [Test]
        public async Task BeforeAsyncIsProcessedCorrectly()
        {
            var model = TestModel.GetModel();

            await _businessLogic.BeforeAsync(ChangeState.Added, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.BeforeAsync(ChangeState.Modified, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.BeforeAsync(ChangeState.Deleted, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.BeforeCreateAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.BeforeUpdateAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.BeforeDeleteAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);
        }

        [Test]
        public async Task AfterAsyncIsProcessedCorrectly()
        {
            var model = TestModel.GetModel();

            await _businessLogic.AfterAsync(ChangeState.Added, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.AfterAsync(ChangeState.Modified, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.AfterAsync(ChangeState.Deleted, model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.AfterCreateAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.AfterUpdateAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);

            await _businessLogic.AfterDeleteAsync(model, CancellationToken.None);
            model.Name.Should().Be(model.Name);
        }

        private class TestBusinessLogic : IBusinessLogic<TestModel>;
    }
}
