using System.Diagnostics;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers
{
    public class SimpleResourceCreateHandler<TResource> : ICreateHandler<TResource> where TResource : class, IGuidIdentifier
    {
        private readonly ActivitySource _activitySource = new($"{nameof(SimpleResourceCreateHandler<TResource>)}<{typeof(TResource)}>");
        private readonly IRepository<TResource> _repository;
        private readonly IValidator<TResource> _validator;

        public SimpleResourceCreateHandler(IRepository<TResource> repository, ICreateValidator<TResource> validator)
        {
            _repository = repository;
            _validator = new GenericValidator<TResource>(validator.ValidateOnCreate);
        }

        public async Task<TResource> CreateAsync(TResource request, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity(nameof(CreateAsync), kind: ActivityKind.Internal, parentContext: default);
            await _validator.ValidateOrThrowAsync(request, cancellationToken);
            request.Id = Guid.NewGuid();
            return await _repository.AddAsync(request, cancellationToken);
        }
    }
}
