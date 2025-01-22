using Dosaic.Plugins.Persistence.Abstractions;
using Dosaic.Testing.NUnit.Models;

namespace Dosaic.Plugins.Persistence.MongoDb
{
    public record TestEntity(Guid Id, string Name, DateTime CreationDate) : BaseTestEntity(Id, Name, CreationDate),
        IGuidIdentifier, ICreationDate;
}
