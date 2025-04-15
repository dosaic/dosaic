using System.Diagnostics;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers
{
    public class SimpleResourceUpdateHandler<TResource> : IUpdateHandler<TResource> where TResource : class, IIdentifier<Guid>
    {
        private readonly ActivitySource _activitySource = new($"{nameof(SimpleResourceUpdateHandler<TResource>)}<{typeof(TResource)}>");
        private readonly IRepository<TResource, Guid> _repository;
        private readonly IValidator<TResource> _validator;

        public SimpleResourceUpdateHandler(IRepository<TResource, Guid> repository, IUpdateValidator<TResource> validator)
        {
            _repository = repository;
            _validator = new GenericValidator<TResource>(validator.ValidateOnUpdate);
        }

        public async Task<TResource> UpdateAsync(TResource request, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity(nameof(UpdateAsync), kind: ActivityKind.Internal, parentContext: default);
            activity!.AddTag("resource-id", request.Id);
            await _validator.ValidateOrThrowAsync(request, cancellationToken);
            var existingResource = await _repository.FindByIdAsync(request.Id, cancellationToken);
            if (existingResource is null)
                throw new DosaicException($"Could not find {typeof(TResource).Name} for id '{request.Id}'");
            return await _repository.UpdateAsync(request, cancellationToken);
        }
    }
}
