using Dosaic.Extensions.Abstractions;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers
{
    public interface IGetListHandler<TResource> : IHandler
    {
        Task<PagedList<TResource>> GetListAsync(PagingRequest request, CancellationToken cancellationToken);
    }
}
