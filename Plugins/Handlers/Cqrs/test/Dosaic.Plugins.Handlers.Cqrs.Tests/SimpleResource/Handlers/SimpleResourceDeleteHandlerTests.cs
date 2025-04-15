using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models;
using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests.SimpleResource.Handlers
{
    public class SimpleResourceDeleteHandlerTests
    {
        private IRepository<TestEntity, Guid> _repository = null!;
        private IDeleteHandler<TestEntity> _handler = null!;

        [SetUp]
        public void Init()
        {
            ActivityTestBootstrapper.Setup();
            _repository = Substitute.For<IRepository<TestEntity, Guid>>();
            _handler = new Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers.SimpleResourceDeleteHandler<TestEntity>(_repository);
        }

        [Test]
        public async Task ReturnsAValidationErrorIfRequestIsInvalid()
        {
            var identifier = Identifier.Empty;
            await _handler.Invoking(async x => await x.DeleteAsync(identifier, CancellationToken.None))
                .Should().ThrowAsync<ValidationDosaicException>();
        }

        [Test]
        public async Task DeleteWorks()
        {
            var identifier = Identifier.New;
            await _handler.DeleteAsync(identifier, CancellationToken.None);
            await _repository.Received(1).RemoveAsync(identifier.Id, CancellationToken.None);
        }
    }
}
