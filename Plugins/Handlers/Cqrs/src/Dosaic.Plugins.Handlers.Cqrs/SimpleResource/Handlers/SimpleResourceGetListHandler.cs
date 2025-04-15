using System.Diagnostics;
using Dosaic.Extensions.Abstractions;
using Dosaic.Hosting.Abstractions.DependencyInjection;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;
using Dosaic.Plugins.Persistence.Abstractions;
using FluentValidation;

namespace Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Handlers
{
    public class SimpleResourceGetListHandler<TResource> : IGetListHandler<TResource>
        where TResource : class, IIdentifier<Guid>
    {
        private readonly ActivitySource _activitySource = new($"{nameof(SimpleResourceGetListHandler<TResource>)}<{typeof(TResource)}>");
        private readonly IFactory<IReadRepository<TResource, Guid>> _repositoryFactory;
        private readonly IValidator<PagingRequest> _validator;

        public SimpleResourceGetListHandler(IFactory<IReadRepository<TResource, Guid>> repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
            _validator = new GenericValidator<PagingRequest>(PagingRequestValidator.Validate);
        }

        public async Task<PagedList<TResource>> GetListAsync(PagingRequest request, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity(nameof(GetListAsync), kind: ActivityKind.Internal, parentContext: default);
            await _validator.ValidateOrThrowAsync(request, cancellationToken);
            var queryOptions = QueryOptions<TResource>.Parse(request.Filter, request.Sort, request.Size, request.Page);
            var totalCountTask = _repositoryFactory.Create().CountAsync(queryOptions, cancellationToken);
            var resultTask = _repositoryFactory.Create().FindAsync(queryOptions, cancellationToken);
            var totalCount = await totalCountTask;
            var result = await resultTask;
            return new PagedList<TResource>(result, totalCount, queryOptions.Page.Value, queryOptions.Size.Value);
        }
    }
}
