using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers
{
    // ReSharper disable once UnusedTypeParameter
#pragma warning disable S2326
    public interface IDeleteHandler<in TResource> : IHandler
    {
        Task DeleteAsync(IIdentifier<Guid> request, CancellationToken cancellationToken);
    }
#pragma warning restore S2326
}
