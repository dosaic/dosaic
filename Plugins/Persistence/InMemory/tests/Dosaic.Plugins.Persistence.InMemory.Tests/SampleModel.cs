using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Persistence.InMemory.Tests
{
    public record SampleModel : IIdentifier<Guid>, ICreationDate
    {
        public Guid Id { get; set; }

        public Guid NewId()
        {
            return Guid.NewGuid();
        }

        public string Name { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
