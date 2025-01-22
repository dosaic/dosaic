using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers;
using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing;
using Dosaic.Testing.NUnit;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests.SimpleResource.Handlers
{
    public class SimpleResourceGetHandlerTests
    {
        private IReadRepository<TestEntity> _repository = null!;
        private IGetHandler<TestEntity> _handler = null!;

        [SetUp]
        public void Init()
        {
            ActivityTestBootstrapper.Setup();
            _repository = Substitute.For<IReadRepository<TestEntity>>();
            _handler = new SimpleResourceGetHandler<TestEntity>(_repository);
        }

        [Test]
        public async Task ReturnsAValidationErrorWhenTheRequestIsInvalid()
        {
            await _handler.Invoking(async x => await x.GetAsync(GuidIdentifier.Empty, CancellationToken.None))
                .Should().ThrowAsync<ValidationDosaicException>();
        }

        [Test]
        public async Task GetWorks()
        {
            var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Sample" };
            _repository.FindByIdAsync(entity.Id, CancellationToken.None)
                .Returns(entity);
            var identifier = new GuidIdentifier(entity.Id);
            var result = await _handler.GetAsync(identifier, CancellationToken.None);
            result.Should().Be(entity);
        }

        [Test]
        public async Task GetWorksCanReturnNotFound()
        {
            var id = Guid.NewGuid();
            _repository.FindByIdAsync(id, CancellationToken.None)
                .Throws(new DosaicException("not found", 404));
            var identifier = new GuidIdentifier(id);
            await _handler.Invoking(async x => await x.GetAsync(identifier, CancellationToken.None))
                .Should().ThrowAsync<DosaicException>();
        }
    }
}
