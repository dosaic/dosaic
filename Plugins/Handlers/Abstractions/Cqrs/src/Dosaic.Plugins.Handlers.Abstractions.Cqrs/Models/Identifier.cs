using Dosaic.Plugins.Persistence.Abstractions;

namespace Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models
{
    public record Identifier(Guid Id) : IIdentifier<Guid>
    {
        public Guid Id { get; set; } = Id;
        public Guid NewId() => Guid.NewGuid();

        public static Identifier Empty => new(Guid.Empty);
        public static Identifier New => new(Guid.NewGuid());
        public static Identifier Parse(string value) => new(Guid.Parse(value));
    }
}
