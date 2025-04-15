using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.Tests
{
    public class TestEntity : IIdentifier<Guid>
    {
        public Guid Id { get; set; }
        public Guid NewId() => Guid.NewGuid();

        public string Name { get; set; }
    }

    public class CustomDeleteHandler : IDeleteHandler<TestEntity>
    {
        private readonly IRepository<TestEntity, Guid> _repository;

        public CustomDeleteHandler(IRepository<TestEntity, Guid> repository)
        {
            _repository = repository;
        }

        public Task DeleteAsync(IIdentifier<Guid> request, CancellationToken cancellationToken)
        {
            return _repository.RemoveAsync(request.Id, cancellationToken);
        }
    }

    public class CustomValidator : ICreateValidator<TestEntity>,
        IUpdateValidator<TestEntity>,
        IDeleteValidator<TestEntity>,
        IGetValidator<TestEntity>,
        IGetListValidator<TestEntity>
    {
        public Action<AbstractValidator<TestEntity>> ValidationRule { get; set; }

        public CustomValidator()
        {
            ValidationRule = validator => validator.RuleFor(x => x.Name).NotNull().MinimumLength(3);
        }
        public CustomValidator(Action<AbstractValidator<TestEntity>> validationRule)
        {
            ValidationRule = validationRule;
        }

        private void Validate(AbstractValidator<TestEntity> validator)
        {
            ValidationRule.Invoke(validator);
        }

        public void ValidateOnCreate(AbstractValidator<TestEntity> validator) => Validate(validator);
        public void ValidateOnUpdate(AbstractValidator<TestEntity> validator) => Validate(validator);
        public void ValidateOnDelete(AbstractValidator<TestEntity> validator) => Validate(validator);
        public void ValidateOnGet(AbstractValidator<TestEntity> validator) => Validate(validator);
        public void ValidateOnGetList(AbstractValidator<TestEntity> validator) => Validate(validator);
    }
}
