using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers;
using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests.SimpleResource.Handlers
{
    public class SimpleResourceUpdateHandlerTests
    {
        private IRepository<TestEntity, Guid> _repository = null!;
        private IUpdateHandler<TestEntity> _handler = null!;
        private CustomValidator _validator = null!;

        [SetUp]
        public void Init()
        {
            ActivityTestBootstrapper.Setup();
            _repository = Substitute.For<IRepository<TestEntity, Guid>>();
            _validator = new CustomValidator();
            _handler = new SimpleResourceUpdateHandler<TestEntity>(_repository, _validator);
        }

        [Test]
        public async Task ReturnsAValidationErrorIfRequestIsInvalid()
        {
            var entity = new TestEntity { Id = Guid.NewGuid(), Name = "1" };
            await _handler.Invoking(async x => await x.UpdateAsync(entity, CancellationToken.None))
                .Should().ThrowAsync<ValidationDosaicException>();
        }

        [Test]
        public async Task ReturnsTheErrorIfTheIdDoesNotExists()
        {
            var entity = new TestEntity { Id = Guid.NewGuid(), Name = "123" };
            _repository.FindByIdAsync(entity.Id, CancellationToken.None)
                .Throws(new DosaicException("not found", 404));
            _repository.UpdateAsync(entity, CancellationToken.None).Returns(entity);
            await _handler.Invoking(async x => await x.UpdateAsync(entity, CancellationToken.None))
                .Should().ThrowAsync<DosaicException>();
        }

        [Test]
        public async Task UpdateWorks()
        {
            var entity = new TestEntity { Id = Guid.NewGuid(), Name = "123" };
            _repository.FindByIdAsync(entity.Id, CancellationToken.None).Returns(entity);
            _repository.UpdateAsync(entity, CancellationToken.None).Returns(entity);
            var result = await _handler.UpdateAsync(entity, CancellationToken.None);
            result.Should().NotBeNull();
            await _repository.Received(1).UpdateAsync(entity, CancellationToken.None);
        }
    }
}
