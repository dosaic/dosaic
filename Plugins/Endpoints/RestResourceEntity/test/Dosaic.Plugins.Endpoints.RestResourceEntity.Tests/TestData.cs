using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity.Tests
{
    public record TestEntity(Guid Id, string Name) : IGuidIdentifier
    {
        public Guid Id { get; set; } = Id;
    }
}
