using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers
{
    public interface IGetHandler<TResource> : IHandler
    {
        Task<TResource> GetAsync(IIdentifier<Guid> request, CancellationToken cancellationToken);
    }
}
