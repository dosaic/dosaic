using System.Diagnostics;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers
{
    public class SimpleResourceDeleteHandler<TResource> : IDeleteHandler<IIdentifier<Guid>> where TResource : class, IIdentifier<Guid>
    {
        private readonly ActivitySource _activitySource = new($"{nameof(SimpleResourceDeleteHandler<TResource>)}<{typeof(TResource)}>");
        private readonly IRepository<TResource, Guid> _repository;
        private readonly IValidator<IIdentifier<Guid>> _validator;

        public SimpleResourceDeleteHandler(IRepository<TResource, Guid> repository)
        {
            _repository = repository;
            _validator = new GenericValidator<IIdentifier<Guid>>(GuidIdentifierValidator.Validate);
        }

        public async Task DeleteAsync(IIdentifier<Guid> request, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity(nameof(DeleteAsync), kind: ActivityKind.Internal, parentContext: default);
            activity!.AddTag("resource-id", request.Id);
            await _validator.ValidateOrThrowAsync(request, cancellationToken);
            await _repository.RemoveAsync(request.Id, cancellationToken);
        }
    }
}
