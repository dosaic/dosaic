using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity.Tests
{
    public record TestEntity(Guid Id, string Name) : IIdentifier<Guid>
    {
        public Guid Id { get; set; } = Id;
        public Guid NewId() => Guid.NewGuid();
    }
}
