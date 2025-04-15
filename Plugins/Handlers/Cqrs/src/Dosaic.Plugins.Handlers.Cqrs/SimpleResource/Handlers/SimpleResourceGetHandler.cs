using System.Diagnostics;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers
{
    public class SimpleResourceGetHandler<TResource> : IGetHandler<TResource>
        where TResource : class, IIdentifier<Guid>
    {
        private readonly ActivitySource _activitySource = new($"{nameof(SimpleResourceGetHandler<TResource>)}<{typeof(TResource)}>");
        private readonly IReadRepository<TResource, Guid> _repository;
        private readonly IValidator<IIdentifier<Guid>> _validator;

        public SimpleResourceGetHandler(IReadRepository<TResource, Guid> repository)
        {
            _repository = repository;
            _validator = new GenericValidator<IIdentifier<Guid>>(GuidIdentifierValidator.Validate);
        }

        public async Task<TResource> GetAsync(IIdentifier<Guid> request, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity(nameof(GetAsync), kind: ActivityKind.Internal, parentContext: default);
            activity!.AddTag("resource-id", request);
            await _validator.ValidateOrThrowAsync(request, cancellationToken);
            return await _repository.FindByIdAsync(request.Id, cancellationToken);
        }
    }
}
