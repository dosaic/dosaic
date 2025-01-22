using RestEase;

namespace Dosaic.Extensions.RestEase.Tests
{
    public interface ISomeApi
    {
        [Post]
        Task<SomeResource> Create([Body] SomeResource dto, CancellationToken cancellationToken);

        [Put("{id}")]
        Task Update([Path] Guid id, [Body] SomeResource dto, CancellationToken cancellationToken);

        [Delete("{id}")]
        Task Delete([Path] Guid id, CancellationToken cancellationToken);

        [Get]
        Task<IList<SomeResource>> Get(CancellationToken cancellationToken);

        [Get("{id}")]
        Task<IList<SomeResource>> GetById([Path] Guid id, CancellationToken cancellationToken);
    }
}
