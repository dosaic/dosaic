using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers;
using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing.NUnit;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests.SimpleResource.Handlers
{
    public class SimpleResourceCreateHandlerTests
    {
        private IRepository<TestEntity> _repository = null!;
        private CustomValidator _validator = null!;
        private ICreateHandler<TestEntity> _handler = null!;

        [SetUp]
        public void Init()
        {
            ActivityTestBootstrapper.Setup();
            _repository = Substitute.For<IRepository<TestEntity>>();
            _validator = new CustomValidator();
            _handler = new SimpleResourceCreateHandler<TestEntity>(_repository, _validator);
        }

        [Test]
        public async Task ReturnsAValidationErrorIfRequestIsInvalid()
        {
            _validator.ValidationRule = validator => validator.RuleFor(x => x.Name).MinimumLength(100);
            var entity = new TestEntity { Name = "1" };
            await _handler.Invoking(async x => await x.CreateAsync(entity, CancellationToken.None))
                .Should().ThrowAsync<ValidationDosaicException>();
        }

        [Test]
        public async Task CreateWorks()
        {
            var entity = new TestEntity { Name = "123", Id = Guid.Empty };
            _repository.AddAsync(Arg.Any<TestEntity>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => callInfo.Arg<TestEntity>());
            var result = await _handler.CreateAsync(entity, CancellationToken.None);
            result.Name.Should().Be("123");
            result.Id.Should().NotBe(Guid.Empty);
            await _repository.Received(1).AddAsync(entity, CancellationToken.None);
            entity.Id.Should().NotBe(Guid.Empty);
        }
    }
}
